using System.Linq;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// m8 "Contracts, Artifacts, Provenance" — the artifact-protocol certification: the 7 canonical lifecycle
/// artifacts are written DURABLY to their exact <see cref="OrchestrationArtifactPaths"/> paths by the producing
/// operation, and a process RESTART (a second orchestrator over the same store + a fresh cache, cold in-memory
/// caches) reconstructs lifecycle state purely from those durable artifacts — plan existence, the next
/// handoff/decisions ordinals, and the one-way re-execution guard — never from reset in-memory counters.
///
/// The seven canonical artifacts certified here (EXACT constants):
///   .agents/specs/epic.md             (OrchestrationArtifactPaths.SpecsEpic)
///   .agents/specs/s{n}.md                (OrchestrationArtifactPaths.Spec(n), 1-based)
///   .agents/plan.md                      (OrchestrationArtifactPaths.Plan)
///   .agents/operational_context.md       (OrchestrationArtifactPaths.OperationalContext)
///   .agents/handoffs/handoff.000N.md     (OrchestrationArtifactPaths.HistoricalHandoff(n))
///   .agents/decisions/decisions.000N.md  (OrchestrationArtifactPaths.HistoricalDecision(n))
///   .agents/operational_delta.md         (OrchestrationArtifactPaths.OperationalDelta)
/// </summary>
public sealed class OrchestrationArtifactProtocolTests
{
    private const string Plan = "PLAN BODY";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    // ---------------------------------------------------------------------------------------------------------
    // 1. DURABILITY — each artifact is written to its canonical path by the producing operation.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Write_plan_persists_epic_and_specs_before_the_turn_and_plan_after()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // The planning turn (Codex) renders .agents/plan.md as its side effect.
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);

        await orchestrator.BeginWritePlanAsync(
            repository,
            new PlanWriteRequest { Epic = "EPIC", Specs = new[] { "SPEC ONE", "SPEC TWO" } });
        await orchestrator.PlanningTurnTask;

        // epic.md + s1.md + s2.md were written to their canonical paths (1-based specs).
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.SpecsEpic), store.WriteQueries);
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.Spec(1)), store.WriteQueries);
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.Spec(2)), store.WriteQueries);
        Assert.Equal("EPIC", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.SpecsEpic)));
        Assert.Equal("SPEC ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Spec(1))));
        Assert.Equal("SPEC TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Spec(2))));

        // The epic/specs were persisted BEFORE the plan (the OnTurn that writes plan.md).
        int epicIndex = store.WriteQueries.IndexOf(Resolve(repository, OrchestrationArtifactPaths.SpecsEpic));
        int planIndex = store.WriteQueries.IndexOf(Resolve(repository, OrchestrationArtifactPaths.Plan));
        Assert.True(epicIndex >= 0 && planIndex >= 0 && epicIndex < planIndex,
            "epic.md must be persisted before plan.md is written by the turn");
        Assert.Equal(Plan, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task Execute_plan_copies_plan_to_operational_context_and_rotates_the_live_handoff()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, FirstHandoff);

        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // plan -> operational_context.md, and the live handoff rotated to handoff.0001.md.
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), store.WriteQueries);
        Assert.Equal(Plan, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));

        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), store.WriteQueries);
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        // The live handoff was consumed by the rotation.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));
    }

    [Fact]
    public async Task Submit_decisions_rotates_decisions_000N_and_rewrites_canonical_decisions_before_the_continuation()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // The continuation one-shot records that BOTH decision artifacts are already on disk at turn time,
        // proving the rotation+canonical rewrite happened-before the continuation turn ran.
        bool decisionsPersistedAtTurn = false;
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            decisionsPersistedAtTurn =
                await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))) &&
                await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions));
            await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO");
        }));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // decisions.0001.md rotated AND canonical decisions.md rewritten, both to their exact paths.
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1)), store.WriteQueries);
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.Decisions), store.WriteQueries);
        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));
        Assert.True(decisionsPersistedAtTurn, "both decision artifacts must be durable before the continuation turn runs");
    }

    [Fact]
    public async Task Transfer_writes_operational_delta_and_rewrites_operational_context()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // Prime the warm decision process (transfer eligibility) via a prior Continue decision run.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS"));  // initial proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        // Script the happy-path transfer: continuation, context rewrite, then delta/reseed/proposal.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "REWRITTEN CONTEXT"))); // rewritten INSIDE the sandbox (Stage 2)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "OPERATIONAL DELTA"));  // ProduceOperationalDelta
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                             // reseed-from-transfer
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));     // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // operational_delta.md was written and operational_context.md rewritten — both to their canonical paths.
        Assert.Contains(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta), store.WriteQueries);
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("REWRITTEN CONTEXT", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task All_seven_canonical_artifacts_resolve_to_their_documented_repository_relative_paths()
    {
        // Lock the EXACT canonical path strings the protocol certifies — a constant rename would break recovery
        // and every independent consumer, so it must break this test.
        Assert.Equal(".agents/specs/epic.md", OrchestrationArtifactPaths.SpecsEpic);
        Assert.Equal(".agents/specs/s1.md", OrchestrationArtifactPaths.Spec(1));
        Assert.Equal(".agents/plan.md", OrchestrationArtifactPaths.Plan);
        Assert.Equal(".agents/operational_context.md", OrchestrationArtifactPaths.OperationalContext);
        Assert.Equal(".agents/handoffs/handoff.0001.md", OrchestrationArtifactPaths.HistoricalHandoff(1));
        Assert.Equal(".agents/decisions/decisions.0001.md", OrchestrationArtifactPaths.HistoricalDecision(1));
        Assert.Equal(".agents/operational_delta.md", OrchestrationArtifactPaths.OperationalDelta);
        Assert.Equal(".agents/deltas", OrchestrationArtifactPaths.DeltasDirectory);
        Assert.Equal(".agents/deltas/operational_delta.0001.md", OrchestrationArtifactPaths.HistoricalDelta(1));
        Assert.Equal(".agents/deltas/operational_delta.0042.md", OrchestrationArtifactPaths.HistoricalDelta(42));
    }

    // ---------------------------------------------------------------------------------------------------------
    // 2. RECOVERABILITY — a restart (fresh orchestrator + fresh cache over the SAME store) reconstructs from disk.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Restart_reports_plan_exists_purely_from_the_durable_plan_artifact()
    {
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();

        // .agents/plan.md is the only durable state; no live planning session exists on the restarted orchestrator.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);

        RepositoryOrchestrator restarted = OrchestrationTestFactory.Orchestrator(
            runtime: new FakeAgentRuntime(),
            store: store,
            cache: OrchestrationTestFactory.Cache()); // FRESH cache: cold in-memory caches, like a process restart

        PlanStatus status = await restarted.GetPlanStatusAsync(repository);

        Assert.True(status.PlanExists);
        Assert.Equal(PlanLifecycleState.ExecutingPlan, status.State);
        Assert.False(restarted.HasPlanningSession); // reconstructed from disk, not a live handle
    }

    [Fact]
    public async Task Restart_recomputes_the_next_handoff_and_decisions_ordinals_from_disk_not_a_reset_counter()
    {
        // A fresh orchestrator (in-memory counters lost) must continue the persisted handoff + decisions sequence
        // from the highest existing ordinal on disk, never clobber decisions.0001 / handoff.0001.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();

        // Prior durable history: plan, operational context, decisions 0001-0002, handoffs 0001-0003.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1)), "OLD D1");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(2)), "OLD D2");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), "H1");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2)), "H2");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(3)), "H3");

        RepositoryOrchestrator restarted = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store, cache: OrchestrationTestFactory.Cache());
        restarted.RecordPlan(Plan);

        // The continuation one-shot writes the next live handoff.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "H4")));

        await restarted.BeginSubmitDecisionsAsync(repository, "D3");
        await restarted.ExecutionRunTask;
        await restarted.DecisionRunTask;

        // The submission continued the sequence: decisions.0003.md and a rotated handoff.0004.md — the 0001 history
        // was NOT clobbered.
        Assert.Equal("D3", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(3))));
        Assert.Equal("H4", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(4))));
        Assert.Equal("OLD D1", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("H1", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Restart_rejects_re_execution_when_a_historical_handoff_already_exists_on_disk()
    {
        // The one-way re-execution guard is reconstructed from disk: a restarted orchestrator must reject
        // BeginExecutePlanAsync (409 / InvalidOperationException) when ANY rotated handoff.*.md is already present.
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        RepositoryOrchestrator restarted = OrchestrationTestFactory.Orchestrator(
            runtime: new FakeAgentRuntime(), store: store, cache: OrchestrationTestFactory.Cache());

        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.BeginExecutePlanAsync(repository));
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task WritePlanAsync(
        RepositoryOrchestrator orchestrator,
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository)
    {
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;
        runtime.OnTurn = null; // operational one-shots are driven by OneShotTurns, not OnTurn
    }

    private static void ScriptMilestoneExtraction(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        params string[] milestoneFileNames) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            foreach (string name in milestoneFileNames)
            {
                await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/{name}"), "milestone");
            }
        }));

    private static void ScriptStartExecution(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        string handoff) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff)));
}

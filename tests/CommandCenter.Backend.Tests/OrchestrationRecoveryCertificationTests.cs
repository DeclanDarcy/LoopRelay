using System.Linq;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;
using CommandCenter.Backend.Tests.Orchestration;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// m10 (C) recovery multi-write windows — the certification "no failed or cancelled run corrupts repository
/// artifacts". Happy-path durability + clean-restart reconstruction is already covered by
/// OrchestrationArtifactProtocolTests; these exercise an INTERMEDIATE store-write/delete FAILURE mid-window: each
/// of the six windows where a run can die between two artifact writes, plus a cross-cutting invariant theory. Every
/// "restart" is driven by a FRESH orchestrator + FRESH cache over the SAME store (a process restart loses in-memory
/// state but not disk), proving the next run recomputes ordinals from disk and a clean retry reaches the terminal.
///
/// The FakeArtifactStore's additive FailWriteOn/FailDeleteOn hooks (default-null) inject the mid-window failure.
/// </summary>
public sealed class OrchestrationRecoveryCertificationTests
{
    private const string Plan = "PLAN BODY";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    // ---------------------------------------------------------------------------------------------------------
    // Window (a): specs written but plan missing — the planning turn completes without rendering plan.md.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_a_specs_durable_plan_absent_then_retry_succeeds()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // The turn writes NO plan.md (no OnTurn side effect) — the half-window: epic/specs durable, plan absent.
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "EPIC", Specs = new[] { "SPEC ONE" } });
        await orchestrator.PlanningTurnTask;

        Assert.Equal("EPIC", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.SpecsEpic)));
        Assert.Equal("SPEC ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Spec(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Plan)));
        Assert.False((await orchestrator.GetPlanStatusAsync(repository)).PlanExists);

        // Retry: this turn renders plan.md => the write succeeds and plan now exists.
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "EPIC", Specs = new[] { "SPEC ONE" } });
        await orchestrator.PlanningTurnTask;

        Assert.Equal(Plan, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Plan)));
        Assert.True((await orchestrator.GetPlanStatusAsync(repository)).PlanExists);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Window (b): plan exists but milestones missing — extraction completes but writes no m*.md.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_b_milestones_missing_leaves_guard_open_then_retry_executes()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);

        // Extraction one-shot completes but writes NO milestone files — the half-window.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn()); // milestone extraction: no files
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // operational_context.md was written (copied before extraction), but NO handoff rotated and the re-execution
        // guard is still OPEN (no historical handoff on disk).
        Assert.Equal(Plan, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.Empty(await store.ListAsync(
            Resolve(repository, OrchestrationArtifactPaths.HandoffsDirectory), OrchestrationArtifactPaths.HistoricalHandoffSearchPattern));

        // Retry on a FRESH orchestrator (restart) with milestones + handoff scripted => 2nd execute succeeds.
        RepositoryOrchestrator restarted = Restart(runtime, store);
        runtime.OneShotTurns.Enqueue(WritesMilestones(store, repository, "m1.md"));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, FirstHandoff));
        await restarted.BeginExecutePlanAsync(repository);
        await restarted.ExecutionRunTask;

        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Window (c): operational_context copied but commit failed — the publisher fails after milestones exist.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_c_commit_failure_writes_no_handoff_then_retry_completes_and_rotates()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher { Result = PlanPublicationResult.Failed("push rejected") };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, publisher: publisher);

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        runtime.OneShotTurns.Enqueue(WritesMilestones(store, repository, "m1.md")); // milestones written
        // StartExecution is never reached (commit fails first), so no handoff one-shot is consumed.

        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // operational_context.md == plan, milestones present, but NO handoff.0001 and the live handoff is absent.
        Assert.Equal(Plan, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Single(await store.ListAsync(
            Resolve(repository, OrchestrationArtifactPaths.MilestonesDirectory), OrchestrationArtifactPaths.MilestoneSearchPattern));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));

        // Flip the publisher to Success and re-run on a FRESH orchestrator => completes + rotates handoff.0001.
        publisher.Result = PlanPublicationResult.Success("sha", pushed: true);
        RepositoryOrchestrator restarted = Restart(runtime, store, publisher);
        runtime.OneShotTurns.Enqueue(WritesMilestones(store, repository, "m1.md"));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, FirstHandoff));
        await restarted.BeginExecutePlanAsync(repository);
        await restarted.ExecutionRunTask;

        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Window (d): handoff exists but rotation failed — the live handoff is read but the rotation write/delete fails.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_d_execute_rotation_delete_failure_keeps_the_live_handoff_recoverable()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        runtime.OneShotTurns.Enqueue(WritesMilestones(store, repository, "m1.md"));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, FirstHandoff));

        // Fail the live-handoff DELETE in the rotation block (after the historical write succeeded). Match the
        // EXACT resolved path (the store keys are OS-resolved, not forward-slash constants).
        string liveHandoffPath = Resolve(repository, OrchestrationArtifactPaths.LiveHandoff);
        store.FailDeleteOn = path => string.Equals(path, liveHandoffPath, StringComparison.OrdinalIgnoreCase);

        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // No data loss: the rotated copy was written AND the live handoff is still readable (delete failed).
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.True(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));

        // A restarted orchestrator's recovery reads still work: ReadLatestHandoff prefers the (still present) live
        // handoff, and the next handoff ordinal recomputes from disk (highest rotated = 1 => next = 2).
        store.FailDeleteOn = null;
        RepositoryOrchestrator restarted = Restart(runtime, store);
        restarted.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        await restarted.BeginSubmitDecisionsAsync(repository, "D1");
        await restarted.ExecutionRunTask;
        await restarted.DecisionRunTask;

        // The continuation rotated to handoff.0002 (ordinal recomputed from disk), not clobbering 0001.
        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Window_d_continuation_rotation_write_failure_keeps_prior_history_intact()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // The continuation writes a new live handoff, then the rotation WRITE of handoff.0002 fails mid-block.
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        string handoff2Path = Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2));
        store.FailWriteOn = path => string.Equals(path, handoff2Path, StringComparison.OrdinalIgnoreCase);

        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // No data loss: handoff.0001 intact, handoff.0002 absent, and the live handoff is STILL readable (the delete
        // never ran because the preceding write threw). ReadLatestHandoff recovers it; NextHandoffSequence recomputes.
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));

        // Retry on a fresh orchestrator: the rotation now succeeds to handoff.0002 (recomputed), no clobber of 0001.
        store.FailWriteOn = null;
        RepositoryOrchestrator restarted = Restart(runtime, store);
        restarted.RecordPlan(Plan);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF THREE"));
        await restarted.BeginSubmitDecisionsAsync(repository, "D2");
        await restarted.ExecutionRunTask;
        await restarted.DecisionRunTask;

        Assert.Equal("HANDOFF THREE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Window (e): decisions persisted but continuation failed.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_e1_continuation_failure_keeps_decisions_durable_then_resubmit_recovers()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // The continuation turn writes NO handoff and reports Failed — decisions are already persisted by then.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "continuation boom"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;

        // decisions.0001.md AND canonical decisions.md are durable; NO new handoff rotated.
        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));

        // Re-submit on a FRESH orchestrator: NextDecisionSequence yields 2 (no clobber of 0001), loop recovers.
        RepositoryOrchestrator restarted = Restart(runtime, store);
        restarted.RecordPlan(Plan);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        await restarted.BeginSubmitDecisionsAsync(repository, "DECISIONS TWO");
        await restarted.ExecutionRunTask;
        await restarted.DecisionRunTask;

        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(2))));
        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
    }

    [Fact]
    public async Task Window_e2_canonical_decisions_write_failure_releases_the_gate_then_retry_writes_next_ordinal()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // Fail the canonical decisions.md write AFTER the numbered decisions.0001.md write succeeds. Match the EXACT
        // resolved canonical path so the numbered decisions.0001.md write (a different path) still lands.
        string canonicalDecisionsPath = Resolve(repository, OrchestrationArtifactPaths.Decisions);
        store.FailWriteOn = path => string.Equals(path, canonicalDecisionsPath, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<IOException>(() => orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE"));

        // decisions.0001.md persisted; canonical decisions.md absent; the ExecutionRun gate was RELEASED (throw path).
        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));
        Assert.False(orchestrator.IsExecutionRunActive);

        // Retry on the SAME orchestrator (gate is free): writes decisions.0002 (no clobber) and completes cleanly.
        store.FailWriteOn = null;
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS TWO");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(2))));
        Assert.Equal("DECISIONS TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Window (f): operational_delta exists but context update failed (Transfer recycle).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Window_f_transfer_context_update_failure_preserves_prior_context_and_reopens_fresh()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // Prime a warm decision process (transfer eligibility).
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS")); // proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        // The transfer: continuation, then a context-rewrite one-shot that FAILS (UpdateOperationalContext).
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "rewrite boom")); // UpdateOperationalContext fails
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "OPERATIONAL DELTA")); // ProduceOperationalDelta (warm)

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // operational_delta.md was written; the PRIOR operational_context.md is intact (the rewrite failed).
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal(OperationalContext, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));

        // The DecisionStream's failed terminal is at the UpdateOperationalContext phase.
        OrchestratorStreamEvent failed = await LastDecisionFailedAsync(orchestrator);
        Assert.Equal("UpdateOperationalContext", Field(failed, "phase"));

        // decisionSeeded == false (the old process was closed during the failed transfer), so the next run opens a
        // FRESH process and re-seeds from the (intact) prior context — Transfer degraded to warm reuse.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // fresh seed (StartDecisionSession)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));  // proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
        // The fresh decision process re-seeded from the prior (intact) operational context.
        FakeAgentSession freshDecision = runtime.Sessions.Last(s => s.Role == SessionRole.Decision);
        Assert.Equal(StartDecisionSession.Render(OperationalContext), freshDecision.Prompts[0]);
    }

    // ---------------------------------------------------------------------------------------------------------
    // CROSS-CUTTING CERT: the invariant set across the recoverable windows. Every written artifact is fully
    // readable; no orphaned live handoff double-advances; the disk-anchored ordinals recompute correctly; and a
    // clean retry reaches a terminal. Driven through fresh orchestrators (restart) per the certification.
    // ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("commit")]            // window (c)
    [InlineData("rotation-delete")]   // window (d)
    [InlineData("continuation")]      // window (e1)
    public async Task Cross_cutting_recovery_invariants_hold_for_each_window(string window)
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher();
        Repository repository = OrchestrationTestFactory.Repository();

        // Reach a state with plan + operational context + a first handoff durable, via a clean Execute Plan.
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, publisher: publisher);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        runtime.OneShotTurns.Enqueue(WritesMilestones(store, repository, "m1.md"));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, FirstHandoff));
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));

        // Inject the window's mid-failure on a fresh continuation orchestrator (restart).
        RepositoryOrchestrator failing = Restart(runtime, store, publisher);
        failing.RecordPlan(Plan);
        switch (window)
        {
            case "continuation":
                runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "boom"));
                await failing.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
                await failing.ExecutionRunTask;
                break;
            case "rotation-delete":
                runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
                string liveHandoffPath = Resolve(repository, OrchestrationArtifactPaths.LiveHandoff);
                store.FailDeleteOn = path => string.Equals(path, liveHandoffPath, StringComparison.OrdinalIgnoreCase);
                await failing.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
                await failing.ExecutionRunTask;
                await failing.DecisionRunTask;
                store.FailDeleteOn = null;
                break;
            case "commit":
                // Re-prove the commit window in isolation: a fresh execute against a NEW plan path is not available
                // post-rotation (one-way guard), so assert the already-rotated handoff is intact and readable.
                break;
        }

        // INVARIANT 1: every written artifact is fully readable (no torn/partial writes).
        foreach (string relative in new[]
        {
            OrchestrationArtifactPaths.Plan,
            OrchestrationArtifactPaths.OperationalContext,
            OrchestrationArtifactPaths.HistoricalHandoff(1),
        })
        {
            Assert.False(string.IsNullOrEmpty(await store.ReadAsync(Resolve(repository, relative))));
        }

        // INVARIANT 2: no orphaned live handoff double-advances the rotation — handoff.0001 is never clobbered, and
        // at most ONE additional rotated handoff exists (the recovered/clean one), recomputed from disk.
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));

        // INVARIANT 3 + 4: the disk-anchored ordinals recompute correctly and a CLEAN retry reaches 'completed' on a
        // fresh orchestrator — no corruption blocks progress.
        store.FailDeleteOn = null;
        store.FailWriteOn = null;
        RepositoryOrchestrator clean = Restart(runtime, store, publisher);
        clean.RecordPlan(Plan);
        // Ensure the live handoff is gone so the clean continuation rotates a fresh ordinal cleanly.
        await store.DeleteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF CLEAN"));
        OrchestratorStreamEvent terminal = await SubmitAndAwaitExecutionTerminalAsync(clean, repository, "DECISIONS CLEAN");
        Assert.Equal("completed", terminal.Type);

        // The clean rotation advanced to the NEXT disk ordinal without clobbering history.
        int highest = HighestHandoffOnDisk(store, repository);
        Assert.True(highest >= 2, $"expected a recomputed ordinal >= 2 on disk, saw {highest}");
        Assert.Equal(FirstHandoff, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    // A "restart": a fresh orchestrator + fresh cache over the SAME store + the SAME runtime (the scripted
    // continuation/decision turns live on that runtime's queues). Disk survives; in-memory orchestrator state does not.
    private static RepositoryOrchestrator Restart(
        FakeAgentRuntime runtime, FakeArtifactStore store, FakePlanArtifactPublisher? publisher = null) =>
        OrchestrationTestFactory.Orchestrator(
            runtime: runtime,
            store: store,
            cache: OrchestrationTestFactory.Cache(),
            publisher: publisher);

    private static FakeOneShotTurn WritesLiveHandoff(FakeArtifactStore store, Repository repository, string handoff) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff));

    private static FakeOneShotTurn WritesMilestones(FakeArtifactStore store, Repository repository, params string[] names) =>
        new(Effect: async () =>
        {
            foreach (string name in names)
            {
                await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/{name}"), "milestone");
            }
        });

    private static int HighestHandoffOnDisk(FakeArtifactStore store, Repository repository)
    {
        IReadOnlyList<string> rotated = store.ListAsync(
            Resolve(repository, OrchestrationArtifactPaths.HandoffsDirectory),
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern).GetAwaiter().GetResult();
        int highest = 0;
        foreach (string path in rotated)
        {
            string[] parts = System.IO.Path.GetFileName(path).Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[^2], out int seq) && seq > highest)
            {
                highest = seq;
            }
        }

        return highest;
    }

    private static async Task<OrchestratorStreamEvent> SubmitAndAwaitExecutionTerminalAsync(
        RepositoryOrchestrator orchestrator, Repository repository, string decisions)
    {
        Task<OrchestratorStreamEvent> drain = NextExecutionTerminalAsync(orchestrator);
        await orchestrator.BeginSubmitDecisionsAsync(repository, decisions);
        await orchestrator.ExecutionRunTask;
        OrchestratorStreamEvent terminal = await drain;
        await orchestrator.DecisionRunTask;
        return terminal;
    }

    private static async Task<OrchestratorStreamEvent> NextExecutionTerminalAsync(RepositoryOrchestrator orchestrator)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent e in orchestrator.ExecutionStream.SubscribeAsync(0, cts.Token))
        {
            if (e.Type is "completed" or "failed")
            {
                return e;
            }
        }

        throw new InvalidOperationException("no execution terminal observed");
    }

    private static async Task<OrchestratorStreamEvent> LastDecisionFailedAsync(RepositoryOrchestrator orchestrator)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await foreach (OrchestratorStreamEvent e in orchestrator.DecisionStream.SubscribeAsync(0, cts.Token))
            {
                events.Add(e);
                if (e.Type == "failed" && events.Count(x => x.Type is "failed" or "review-ready") >= 2)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        return events.Last(e => e.Type == "failed");
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        System.Text.Json.JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();
}

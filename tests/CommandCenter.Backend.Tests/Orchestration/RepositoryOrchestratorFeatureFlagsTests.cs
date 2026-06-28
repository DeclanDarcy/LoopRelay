using System.Linq;
using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// m10 hardening/certification feature-flag branches on <see cref="RepositoryOrchestrator"/>. Each flag is
/// additive: a default-constructed <see cref="OrchestrationFeatureFlags"/> reproduces today's behavior, so the
/// rest of the suite (which never passes flags) still exercises the warm/router/commit paths unchanged. These
/// tests flip each flag and assert the off/forced path while keeping every stream frame contract intact.
/// </summary>
public sealed class RepositoryOrchestratorFeatureFlagsTests
{
    private const string PlanBody = "PLAN BODY";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    // ---- (1) Defaults reproduce today's behavior byte-for-byte ----

    [Fact]
    public void Default_flags_reproduce_todays_behavior()
    {
        var flags = new OrchestrationFeatureFlags();

        Assert.True(flags.PersistentPlanningProcessEnabled);
        Assert.True(flags.PersistentDecisionProcessReuseEnabled);
        Assert.False(flags.TransferOnlyDecisionFallbackEnabled);
        Assert.True(flags.AutomaticCommitPushAfterExecuteEnabled);
    }

    // ---- (2a) PersistentPlanningProcessEnabled = false => one-shot planning ----

    [Fact]
    public async Task One_shot_planning_write_runs_a_one_shot_and_holds_no_session()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store,
            flags: new OrchestrationFeatureFlags(PersistentPlanningProcessEnabled: false));

        // The planning prompt now runs via RunOneShotAsync (OneShotTurns), not a held-open RunTurnAsync.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(
            Output: "ignored",
            Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "RENDERED PLAN"),
            Chunks: new[] { "part A ", "part B" }));

        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "r" });
        await orchestrator.PlanningTurnTask;

        // No held-open planning session was opened; the one-shot carried the WritePlan prompt.
        Assert.False(orchestrator.HasPlanningSession);
        Assert.Empty(runtime.Sessions);
        Assert.Equal(WritePlanAgainstCodebase.Text, Assert.Single(runtime.OneShotPrompts));
        Assert.Equal(SessionRole.Planning, Assert.Single(runtime.OneShotSpecs).Role);

        // The stream frame sequence is IDENTICAL to the warm path: turn-started / delta* / completed.
        List<OrchestratorStreamEvent> events = await DrainPlanningAsync(orchestrator.PlanningStream, 4);
        Assert.Equal("turn-started", events[0].Type);
        Assert.Equal("WritePlan", Field(events[0], "phase"));
        Assert.Equal("delta", events[1].Type);
        Assert.Equal("part A ", Field(events[1], "text"));
        Assert.Equal("delta", events[2].Type);
        Assert.Equal("completed", events[3].Type);
        Assert.Equal("RENDERED PLAN", Field(events[3], "plan"));
        Assert.Equal("RENDERED PLAN", orchestrator.CachedPlan);
    }

    [Fact]
    public async Task One_shot_planning_revise_reruns_revise_as_its_own_one_shot_without_a_warm_session()
    {
        // CHOSEN SEMANTICS: in one-shot mode Revise has no warm session; it re-runs RevisePlan as a fresh one-shot
        // against the freshly persisted plan. It must NOT throw "no warm planning session".
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store,
            flags: new OrchestrationFeatureFlags(PersistentPlanningProcessEnabled: false));

        // First a one-shot Write persists the plan.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(
            Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "PLAN V1")));
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "r" });
        await orchestrator.PlanningTurnTask;

        // Then a one-shot Revise re-runs RevisePlan against the persisted plan (rewrites plan.md).
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(
            Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "PLAN V2")));
        await orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "tighten scope" });
        await orchestrator.PlanningTurnTask;

        Assert.Equal(RevisePlan.Render("tighten scope"), runtime.OneShotPrompts[1]);
        Assert.Equal("PLAN V2", orchestrator.CachedPlan);
        // Still no held-open session at any point.
        Assert.False(orchestrator.HasPlanningSession);
        Assert.Empty(runtime.Sessions);
    }

    // ---- (2b) PersistentDecisionProcessReuseEnabled = false => open/seed/propose/close every run ----

    [Fact]
    public async Task Decision_reuse_disabled_opens_a_fresh_process_each_run_and_closes_it_after()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store,
            flags: new OrchestrationFeatureFlags(PersistentDecisionProcessReuseEnabled: false));

        await SeedLoopAsync(orchestrator, store, repository);

        // Run 1: seed + propose, then close.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                       // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS A"));  // proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        Assert.Equal("DECISIONS A", orchestrator.CurrentDecisions);
        // The process was closed after the run (no warm reuse): the orchestrator holds no decision session.
        Assert.False(orchestrator.HasDecisionSession);
        FakeAgentSession first = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.True(first.Disposed);

        // Run 2: a SECOND fresh process is opened and re-seeded (no warm fast-path), then closed.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                       // re-seed (fresh process)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS B"));  // proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        Assert.Equal("DECISIONS B", orchestrator.CurrentDecisions);
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);                // two distinct processes, one per run
        Assert.All(decisionSessions, s => Assert.True(s.Disposed));
        // Each run re-seeded: StartDecisionSession was the first prompt of BOTH processes.
        Assert.Equal(StartDecisionSession.Render(OperationalContext), decisionSessions[1].Prompts[0]);
        Assert.False(orchestrator.HasDecisionSession);
    }

    [Fact]
    public async Task Decision_reuse_disabled_preserves_the_read_only_posture_and_stream_contract()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store,
            flags: new OrchestrationFeatureFlags(PersistentDecisionProcessReuseEnabled: false));

        await SeedLoopAsync(orchestrator, store, repository);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                       // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS"));    // proposal

        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        List<OrchestratorStreamEvent> events = await DrainDecisionTerminalAsync(orchestrator.DecisionStream);
        // The same DecisionStream contract: run-started -> diagnostics(read-only/never) -> ... -> review-ready.
        Assert.Equal("DecisionRun", Field(events[0], "phase"));
        OrchestratorStreamEvent diagnostics = events.Single(e => e.Type == "diagnostics");
        Assert.Equal("read-only", Field(diagnostics, "sandbox"));
        Assert.Equal("never", Field(diagnostics, "approvals"));
        Assert.Equal("review-ready", events[^1].Type);
        // The decision spec is read-only with approvals never (zero operational authority), unchanged by the flag.
        FakeAgentSession decision = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        AgentSessionSpec spec = runtime.OpenedSpecs.Single(s => s.Role == SessionRole.Decision);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.True(decision.Disposed);
    }

    // ---- (2c) TransferOnlyDecisionFallbackEnabled = true => force Transfer (downgrades still apply) ----

    [Fact]
    public async Task Transfer_only_fallback_forces_transfer_even_when_the_router_says_continue()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Continue }; // router would NOT transfer
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store, router: router,
            flags: new OrchestrationFeatureFlags(TransferOnlyDecisionFallbackEnabled: true));

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // primes the warm process (eligible)
        ScriptTransferTurns(runtime, store, repository, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Despite the router returning Continue, the forced fallback transferred: the delta was extracted, the
        // context rewritten, and the old process recycled into a fresh one.
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("NEXT", orchestrator.CurrentDecisions);
        Assert.True(router.EvaluateCount >= 1); // the router still ran (its inputs are recorded)
    }

    [Fact]
    public async Task Transfer_only_fallback_still_downgrades_to_continue_when_the_process_is_unseeded()
    {
        // The forced Transfer must STILL honour the eligibility downgrade: an unseeded process degrades to warm
        // reuse (Continue), never extracts a bogus delta from an empty conversation.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Continue };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store, router: router,
            flags: new OrchestrationFeatureFlags(TransferOnlyDecisionFallbackEnabled: true));

        await SeedLoopAsync(orchestrator, store, repository); // NB: no prior decision run -> unseeded
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));  // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The ineligible (unseeded) forced Transfer degraded to Continue: no delta, a single seeded process.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        FakeAgentSession decision = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.Equal(StartDecisionSession.Render(OperationalContext), decision.Prompts[0]);
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
    }

    // ---- (2d) AutomaticCommitPushAfterExecuteEnabled = false => skip commit/push ----

    [Fact]
    public async Task Auto_commit_disabled_skips_publish_and_committed_frame_and_completes_with_null_commit_sha()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store, publisher: publisher,
            flags: new OrchestrationFeatureFlags(AutomaticCommitPushAfterExecuteEnabled: false));

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF V1");

        Task<List<OrchestratorStreamEvent>> drain = DrainExecutionTerminalAsync(orchestrator.ExecutionStream);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;

        // No commit/push happened, and no `committed` frame was emitted.
        Assert.Equal(0, publisher.PublishCount);
        Assert.DoesNotContain(events, e => e.Type == "committed");
        // The run still proceeded: milestones extracted, lifecycle crossed, handoff rotated, completed terminal.
        Assert.Contains(events, e => e.Type == "milestones-extracted");
        Assert.Contains(events, e => e.Type == "lifecycle");
        Assert.Equal("completed", events[^1].Type);
        // The completed frame keeps its shape; commitSha is present-but-null (the additive skip signal).
        JsonElement completed = JsonDocument.Parse(events[^1].Data).RootElement;
        Assert.True(completed.TryGetProperty("commitSha", out JsonElement sha));
        Assert.Equal(JsonValueKind.Null, sha.ValueKind);
        Assert.Equal("HANDOFF V1", orchestrator.CurrentHandoff);
    }

    [Fact]
    public async Task Auto_commit_enabled_by_default_still_commits_and_emits_committed()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var publisher = new FakePlanArtifactPublisher();
        Repository repository = OrchestrationTestFactory.Repository();
        // No flags passed => default (enabled), proving the default path is untouched.
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(
            runtime: runtime, store: store, publisher: publisher);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, "HANDOFF V1");

        Task<List<OrchestratorStreamEvent>> drain = DrainExecutionTerminalAsync(orchestrator.ExecutionStream);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;
        List<OrchestratorStreamEvent> events = await drain;

        Assert.Equal(1, publisher.PublishCount);
        Assert.Contains(events, e => e.Type == "committed" && Field(e, "commitSha") == "commit-sha");
        Assert.Equal("commit-sha", Field(events[^1], "commitSha"));
    }

    // ---- helpers (mirrors RepositoryOrchestratorTransferTests / RepositoryOrchestratorExecutionTests) ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task WritePlanAsync(
        RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository)
    {
        string planPath = Resolve(repository, OrchestrationArtifactPaths.Plan);
        runtime.OnTurn = () => store.WriteAsync(planPath, PlanBody);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Roadmap = "r" });
        await orchestrator.PlanningTurnTask;
        runtime.OnTurn = null;
    }

    private static async Task SeedLoopAsync(RepositoryOrchestrator orchestrator, FakeArtifactStore store, Repository repository)
    {
        orchestrator.RecordPlan(PlanBody);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
    }

    private static async Task SeedWarmDecisionSessionAsync(RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, Repository repository)
    {
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS"));  // initial proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;
    }

    private static void ScriptTransferTurns(
        FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository,
        string delta, string rewrittenContext, string proposal)
    {
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, rewrittenContext));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: delta));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: proposal));
    }

    private static FakeOneShotTurn WritesLiveHandoff(FakeArtifactStore store, Repository repository, string handoff) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff));

    private static FakeOneShotTurn WritesOperationalContext(FakeArtifactStore store, Repository repository, string context) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), context));

    private static void ScriptMilestoneExtraction(
        FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository, params string[] milestoneFileNames) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            foreach (string name in milestoneFileNames)
            {
                await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/{name}"), "milestone");
            }
        }));

    private static void ScriptStartExecution(FakeAgentRuntime runtime, FakeArtifactStore store, Repository repository, string handoff) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff)));

    private static async Task<List<OrchestratorStreamEvent>> DrainPlanningAsync(OrchestratorStreamChannel stream, int expectedCount)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (events.Count >= expectedCount)
            {
                break;
            }
        }

        return events;
    }

    private static Task<List<OrchestratorStreamEvent>> DrainExecutionTerminalAsync(OrchestratorStreamChannel stream) =>
        DrainUntilAsync(stream, e => e.Type is "completed" or "failed");

    private static Task<List<OrchestratorStreamEvent>> DrainDecisionTerminalAsync(OrchestratorStreamChannel stream) =>
        DrainUntilAsync(stream, e => e.Type is "review-ready" or "failed");

    private static async Task<List<OrchestratorStreamEvent>> DrainUntilAsync(
        OrchestratorStreamChannel stream, Func<OrchestratorStreamEvent, bool> isTerminal)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (isTerminal(streamEvent))
            {
                break;
            }
        }

        return events;
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();
}

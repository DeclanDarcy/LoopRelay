using System.Linq;
using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Services;
using CommandCenter.Orchestration.Streaming;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// Decision Runtime (m5): the held-open zero-permission Decision process proposes decisions over the latest
/// execution handoff. These cover the ordered run (seed off-stream, then GetNextDecisions streamed + captured),
/// the read-only sandbox posture, the seed-once invariant, every failure boundary (no operational context,
/// failed seed, no handoff, failed proposal), the human review/submit gate that persists decisions, and the
/// concurrency + dispose guarantees the independent decision gate provides.
/// </summary>
public sealed class RepositoryOrchestratorDecisionTests
{
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string Handoff = "EXECUTION SESSION REPORT";
    private const string ProposedDecisions = "PROPOSED DECISIONS";

    [Fact]
    public async Task Decision_run_seeds_off_stream_then_proposes_decisions_in_order()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        // The seed turn carries a distinctive marker chunk. Production seeds with onChunk:null, so the marker
        // must NEVER reach a delta — but if a regression ever wired the seed to the primary delta sink, the
        // marker WOULD leak and the off-stream assertion below would fail (without the marker that assertion is
        // tautological: a chunk-less seed can never produce a delta no matter how it is wired).
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Chunks: new[] { "SEED-LEAK-MARKER" }));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(
            Output: ProposedDecisions,
            Chunks: new[] { "PROPOSED ", "DECISIONS" }));

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.DecisionStream, "review-ready", "failed");

        // The held-open Decision process ran exactly two turns: the seed, then the proposal — same session.
        FakeAgentSession decision = runtime.Sessions.Single(session => session.Role == SessionRole.Decision);
        Assert.Equal(StartDecisionSession.Render(OperationalContext), decision.Prompts[0]);
        Assert.Equal(GetNextDecisions.Render(Handoff), decision.Prompts[1]);
        Assert.Equal(2, decision.Prompts.Count);

        // The proposed decisions were captured as transient run state but NOT persisted (no human submit yet).
        Assert.Equal(ProposedDecisions, orchestrator.CurrentDecisions);
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));

        // The stream told the ordered story: started, diagnostics, the GetNextDecisions phase, deltas, then
        // completion and the review-ready gate carrying the editable decisions.
        Assert.Equal("run-started", events[0].Type);
        OrchestratorStreamEvent diagnostics = events.Single(e => e.Type == "diagnostics");
        Assert.Equal("read-only", Field(diagnostics, "sandbox"));
        Assert.Equal("never", Field(diagnostics, "approvals"));
        Assert.True(BoolField(diagnostics, "seeded")); // the prime completed before diagnostics emitted
        Assert.Contains(events, e => e.Type == "phase" && Field(e, "phase") == "GetNextDecisions");
        Assert.Contains(events, e => e.Type == "delta" && Field(e, "text") == "PROPOSED ");
        Assert.Contains(events, e => e.Type == "completed");
        Assert.Equal("review-ready", events[^1].Type);
        Assert.Equal(ProposedDecisions, Field(events[^1], "decisions"));

        // The seed turn was kept off the primary stream — no delta carries the seed's marker chunk.
        Assert.DoesNotContain(events, e => e.Type == "delta" && Field(e, "text")!.Contains("SEED-LEAK-MARKER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Decision_session_uses_a_read_only_zero_permission_sandbox()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions));

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;

        AgentSessionSpec spec = runtime.OpenedSpecs.Single(s => s.Role == SessionRole.Decision);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal("xhigh", spec.Effort.Identifier);
    }

    [Fact]
    public async Task Decision_run_is_rejected_when_no_operational_context_exists()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginDecisionRunAsync(OrchestrationTestFactory.Repository()));
    }

    [Fact]
    public async Task Decision_run_publishes_failed_when_seeding_does_not_complete()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "seed exit code 1"));

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Equal("StartDecisionSession", Field(terminal, "phase"));
        Assert.Equal("The decision seeding run failed.", Field(terminal, "reason"));
        Assert.Equal("seed exit code 1", Field(terminal, "detail"));
        // The poisoned, half-primed process was torn down so a retry re-seeds a fresh one.
        Assert.False(orchestrator.HasDecisionSession);
    }

    [Fact]
    public async Task A_failed_seed_closes_the_process_so_the_next_run_re_seeds_a_fresh_one()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "seed boom")); // run 1 seed fails
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // run 2 seed succeeds
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions)); // run 2 proposal

        // Run 1: the seed fails and the poisoned decision process is torn down (no re-seed of a primed convo).
        OrchestratorStreamEvent firstTerminal = await RunAndAwaitTerminalAsync(orchestrator, repository);
        Assert.Equal("failed", firstTerminal.Type);
        Assert.False(orchestrator.HasDecisionSession);

        // Run 2: a BRAND-NEW decision process is opened and re-seeded, then proposes successfully.
        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;

        Assert.Equal(2, runtime.Sessions.Count); // a fresh process, not the poisoned one
        Assert.True(runtime.Sessions[0].Disposed); // the first (failed-seed) process was disposed
        Assert.Equal(ProposedDecisions, orchestrator.CurrentDecisions);
        // Two genuine seed issuances (one per fresh process), so two StartDecisionSession provenance entries.
        Assert.Equal(2, orchestrator.DecisionProvenance.Count(p => p.PromptName == nameof(StartDecisionSession)));
    }

    [Fact]
    public async Task Decision_run_publishes_failed_when_no_handoff_is_available()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // Operational context exists (seeding can proceed) but no handoff has been produced yet.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn()); // seed completes

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Equal("GetNextDecisions", Field(terminal, "phase"));
        Assert.Contains("No execution handoff is available", Field(terminal, "reason"));
    }

    [Fact]
    public async Task Decision_run_publishes_failed_when_the_proposal_does_not_complete()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn()); // seed completes
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "proposal crashed"));

        OrchestratorStreamEvent terminal = await RunAndAwaitTerminalAsync(orchestrator, repository);

        Assert.Equal("failed", terminal.Type);
        Assert.Equal("GetNextDecisions", Field(terminal, "phase"));
        Assert.Equal("The decision proposal run failed.", Field(terminal, "reason"));
    }

    [Fact]
    public async Task Decision_run_reads_the_live_handoff_when_one_is_present()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // Both a live handoff and a rotated one exist — the live handoff wins.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), "STALE");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "LIVE HANDOFF");
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions));

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;

        FakeAgentSession decision = runtime.Sessions.Single(session => session.Role == SessionRole.Decision);
        Assert.Equal(GetNextDecisions.Render("LIVE HANDOFF"), decision.Prompts[1]);
    }

    [Fact]
    public async Task Decision_run_seeds_only_once_across_repeated_runs()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                       // seed (first run only)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS A"));  // proposal, run 1
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS B"));  // proposal, run 2

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;

        FakeAgentSession decision = runtime.Sessions.Single(session => session.Role == SessionRole.Decision);
        Assert.Single(runtime.Sessions); // the warm decision process was reused, not reopened
        Assert.Equal(3, decision.Prompts.Count);
        Assert.Equal(1, decision.Prompts.Count(p => p == StartDecisionSession.Render(OperationalContext)));
        Assert.Equal(2, decision.Prompts.Count(p => p == GetNextDecisions.Render(Handoff)));
        Assert.Equal("DECISIONS B", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task Submitting_decisions_persists_a_numbered_submission_and_the_live_artifact()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        Task<List<OrchestratorStreamEvent>> drain = DrainUntilAsync(orchestrator.DecisionStream, "submitted");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "REVIEWED DECISIONS");
        List<OrchestratorStreamEvent> events = await drain;
        // Drain the continuation the submit launched (it fails fast here — no handoff to continue from).
        await orchestrator.ExecutionRunTask;

        // The human-approved decisions land BOTH as a numbered submission (history/recovery) and as the live
        // canonical artifact every downstream consumer + the next continuation reads.
        Assert.Equal("REVIEWED DECISIONS", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("REVIEWED DECISIONS", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("REVIEWED DECISIONS", orchestrator.CurrentDecisions);

        OrchestratorStreamEvent submitted = events.Single(e => e.Type == "submitted");
        Assert.Equal(OrchestrationArtifactPaths.Decisions, Field(submitted, "path")); // back-compat: live canonical path
        Assert.Equal(OrchestrationArtifactPaths.HistoricalDecision(1), Field(submitted, "numberedPath"));
        Assert.Equal(1, IntField(submitted, "sequence"));
    }

    [Fact]
    public async Task Submitting_empty_decisions_is_rejected()
    {
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();

        await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.BeginSubmitDecisionsAsync(OrchestrationTestFactory.Repository(), "   "));
    }

    [Fact]
    public async Task Submitting_decisions_after_dispose_throws_object_disposed()
    {
        // ObjectDisposedException derives from InvalidOperationException, so the submit endpoint maps a
        // disposed orchestrator to a recoverable 409 (matching the sibling decision/run endpoint).
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator();
        await orchestrator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => orchestrator.BeginSubmitDecisionsAsync(OrchestrationTestFactory.Repository(), "approved"));
    }

    [Fact]
    public async Task A_second_decision_run_is_rejected_while_one_is_running()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn()); // seed completes
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions, Effect: () => gate.Task)); // proposal parks

        await orchestrator.BeginDecisionRunAsync(repository);
        await WaitForAsync(() => orchestrator.IsDecisionRunActive);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.BeginDecisionRunAsync(repository));

        gate.SetResult();
        await orchestrator.DecisionRunTask;
        Assert.False(orchestrator.IsDecisionRunActive);
    }

    [Fact]
    public async Task A_decision_run_can_overlap_an_execution_run()
    {
        // The decision gate is independent of runState: a read-only decision run is allowed to overlap an
        // in-flight execution run (it has zero operational authority). Park an execution run, then prove a
        // decision run still claims its own gate.
        var executionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        // The LIVE handoff (not a rotated one) — so Execute's re-execution guard, which only matches rotated
        // handoff.*.md, still lets the execution run start while the decision run reads this live handoff.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), Handoff);
        runtime.OneShotGate = executionGate.Task; // park the execution run on its first one-shot
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                      // decision seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions)); // decision proposal

        await orchestrator.BeginExecutePlanAsync(repository);
        await WaitForAsync(() => orchestrator.IsExecutionRunActive);

        // The decision run proceeds despite the execution run holding runState.
        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        Assert.Equal(ProposedDecisions, orchestrator.CurrentDecisions);

        executionGate.SetResult();
        await orchestrator.ExecutionRunTask;
    }

    [Fact]
    public async Task Dispose_drains_an_in_flight_decision_run_before_completing_the_streams()
    {
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        // The proposal turn signals once it is genuinely parked (session opened + seeded), so the dispose
        // drain is observed against a run that is actually in flight — not merely the synchronous gate claim.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions, Effect: () =>
        {
            parked.TrySetResult();
            return release.Task;
        }));

        await orchestrator.BeginDecisionRunAsync(repository);
        await parked.Task;
        Assert.True(orchestrator.IsDecisionRunActive);

        Task dispose = orchestrator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(dispose.IsCompleted); // parked: Dispose is draining the in-flight decision run

        release.SetResult();
        await dispose; // completes cleanly — no publish-after-complete throw

        Assert.True(orchestrator.IsDisposed);
        Assert.True(orchestrator.DecisionStream.IsCompleted);
    }

    [Fact]
    public async Task Decision_provenance_is_recorded_for_the_seed_and_the_proposal()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await SeedAsync(store, repository, OperationalContext, OrchestrationArtifactPaths.HistoricalHandoff(1), Handoff);
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: ProposedDecisions));

        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;

        Assert.Equal(2, orchestrator.DecisionProvenance.Count);

        PromptProvenance seed = orchestrator.DecisionProvenance[0];
        Assert.Equal(nameof(StartDecisionSession), seed.PromptName);
        Assert.Equal(StartDecisionSession.SourceHash, seed.SourceHash);
        Assert.Equal(PromptSessionRole.Decision, seed.SessionRole);
        Assert.Equal("StartDecisionSession", seed.WorkflowPhase);
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(seed.InputArtifactIdentities));
        Assert.Empty(seed.OutputArtifactIdentities);

        PromptProvenance proposal = orchestrator.DecisionProvenance[1];
        Assert.Equal(nameof(GetNextDecisions), proposal.PromptName);
        Assert.Equal(GetNextDecisions.SourceHash, proposal.SourceHash);
        Assert.Equal(PromptSessionRole.Decision, proposal.SessionRole);
        Assert.Equal("GetNextDecisions", proposal.WorkflowPhase);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), Assert.Single(proposal.InputArtifactIdentities));
        Assert.Equal(OrchestrationArtifactPaths.Decisions, Assert.Single(proposal.OutputArtifactIdentities));
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task SeedAsync(
        FakeArtifactStore store,
        Repository repository,
        string operationalContext,
        string handoffRelativePath,
        string handoff)
    {
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), operationalContext);
        await store.WriteAsync(Resolve(repository, handoffRelativePath), handoff);
    }

    private static async Task<OrchestratorStreamEvent> RunAndAwaitTerminalAsync(
        RepositoryOrchestrator orchestrator,
        Repository repository)
    {
        await orchestrator.BeginDecisionRunAsync(repository);
        await orchestrator.DecisionRunTask;
        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.DecisionStream, "review-ready", "failed");
        return events[^1];
    }

    private static async Task<List<OrchestratorStreamEvent>> DrainUntilAsync(
        OrchestratorStreamChannel stream,
        params string[] terminalTypes)
    {
        var events = new List<OrchestratorStreamEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (terminalTypes.Contains(streamEvent.Type))
            {
                break;
            }
        }

        return events;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();

    private static bool BoolField(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetBoolean();

    private static int IntField(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetInt32();
}

using System.Linq;
using System.Text.Json;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Streaming;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// Continuation Loop (m6): submitting reviewed decisions persists a numbered submission + the live artifact,
/// then drives a <c>ContinueExecution</c> turn over the cached plan, latest handoff, and the submitted
/// decisions — rotating the next handoff and routing the next decision run so the UI returns to decision
/// streaming. These cover persistence-before-continuation, the rendered continuation inputs, handoff
/// rotation, the auto-started next decision run, restart-safe sequence recovery from disk, the execution-gate
/// rejection, continuation provenance, two full iterations, and the flow conversation projection.
/// </summary>
public sealed class RepositoryOrchestratorContinuationTests
{
    private const string Plan = "PLAN TEXT";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    [Fact]
    public async Task Continuation_uses_the_cached_plan_latest_handoff_and_submitted_decisions()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan); // the cached plan the spec renders into ContinueExecution
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        // The continuation one-shot writes the next live handoff (Codex's operational output).
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask; // drain the auto-started next decision run too

        // The continuation rendered the cached plan and the submitted decisions — the two inputs ContinueExecution
        // now renders ({handoff} was removed from the template; the handoff is still read as continuation context).
        Assert.Equal(ContinueExecution.Render(Plan, null, "DECISIONS ONE"), runtime.OneShotPrompts[0]);
    }

    [Fact]
    public async Task Continuation_rotates_the_next_handoff()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));

        List<OrchestratorStreamEvent> events = await SubmitAndDrainExecutionAsync(orchestrator, repository, "DECISIONS ONE");
        await orchestrator.DecisionRunTask;

        // The new handoff was rotated to handoff.0002.md and the live handoff was consumed.
        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));

        OrchestratorStreamEvent rotated = events.Single(e => e.Type == "handoff-rotated");
        Assert.Equal(2, IntField(rotated, "sequence"));
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(2), Field(rotated, "path"));
        OrchestratorStreamEvent completed = events.Single(e => e.Type == "completed");
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(2), Field(completed, "handoffPath"));
    }

    [Fact]
    public async Task Submitted_decisions_are_persisted_before_the_continuation_turn_runs()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);

        // The continuation one-shot records persistence visibility at turn time, signals it is parked (so the
        // background run is quiescent and not racing the store), then waits for the test to release it. This
        // pins the cert's happens-before: the submission is persisted before the continuation turn proceeds.
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var turnGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool decisionsPersistedAtTurn = false;
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            decisionsPersistedAtTurn =
                await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))) &&
                await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions));
            parked.TrySetResult();
            await turnGate.Task;
            await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO");
        }));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await parked.Task; // continuation parked INSIDE the turn — its store reads are done

        // The continuation is parked, yet both the numbered submission AND the live canonical decisions are
        // ALREADY observable. They were written synchronously inside submit, before the background continuation
        // was even launched; this would fail if persistence were moved to run concurrently with the continuation.
        Assert.True(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.True(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));

        turnGate.SetResult();
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.True(decisionsPersistedAtTurn); // also visible from inside the turn body
    }

    [Fact]
    public async Task Continuation_returns_to_decision_streaming_by_starting_the_next_decision_run()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        // The auto-started next decision run reuses the warm process: seed then propose over the NEW handoff.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // decision seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));  // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The follow-up decision run proposed over the freshly rotated handoff (handoff.0002 = "HANDOFF TWO").
        FakeAgentSession decision = runtime.Sessions.Single(session => session.Role == SessionRole.Decision);
        Assert.Equal(GetNextDecisions.Render("HANDOFF TWO"), decision.Prompts[1]);
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);

        // The decision stream re-opened a run and reached a fresh review-ready (UI returns to decision streaming).
        List<OrchestratorStreamEvent> events = await DrainUntilAsync(orchestrator.DecisionStream, "review-ready", "failed");
        Assert.Contains(events, e => e.Type == "run-started");
        Assert.Equal("review-ready", events[^1].Type);
        Assert.Equal("NEXT DECISIONS", Field(events[^1], "decisions"));
    }

    [Fact]
    public async Task Recovery_continues_the_decision_and_handoff_sequence_from_disk()
    {
        // A fresh orchestrator (in-memory counters lost) must recover the latest persisted decision + handoff
        // sequence from disk rather than clobbering decisions.0001 / handoff.0001.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan); // restart: plan from disk
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1)), "OLD D1");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(2)), "OLD D2");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), "H1");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2)), "H2");
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(3)), "H3");
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "H4")));

        List<OrchestratorStreamEvent> events = await SubmitAndDrainExecutionAsync(orchestrator, repository, "D3");
        await orchestrator.DecisionRunTask;

        // The submission continued the sequence: decisions.0003.md and a rotated handoff.0004.md.
        Assert.Equal("D3", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(3))));
        Assert.Equal("H4", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(4))));
        Assert.Equal(4, IntField(events.Single(e => e.Type == "handoff-rotated"), "sequence"));
    }

    [Fact]
    public async Task Submitting_decisions_while_an_execution_run_is_active_is_rejected_without_persisting()
    {
        var executionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        // A plan with no rotated handoff lets Execute Plan start; park its first one-shot to hold runState.
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        runtime.OneShotGate = executionGate.Task;

        await orchestrator.BeginExecutePlanAsync(repository);
        await WaitForAsync(() => orchestrator.IsExecutionRunActive);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE"));
        // The rejected submit claimed the gate BEFORE any persistence, so nothing was written.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.Decisions)));

        executionGate.SetResult();
        await orchestrator.ExecutionRunTask;
    }

    [Fact]
    public async Task Continuation_records_provenance_for_the_continue_execution_turn()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        PromptProvenance continuation = orchestrator.ExecutionProvenance
            .Single(p => p.PromptName == nameof(ContinueExecution));
        Assert.Equal(ContinueExecution.SourceHash, continuation.SourceHash);
        Assert.Equal(PromptSessionRole.OperationalExecution, continuation.SessionRole);
        Assert.Equal("ContinueExecution", continuation.WorkflowPhase);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.HistoricalHandoff(1), OrchestrationArtifactPaths.Decisions },
            continuation.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.LiveHandoff, Assert.Single(continuation.OutputArtifactIdentities));
    }

    [Fact]
    public async Task Two_decision_continuation_iterations_persist_sequential_decisions_and_handoffs()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF THREE")));

        // Iteration 1.
        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Iteration 2 — the same screen/orchestrator, no teardown between.
        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS TWO");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Equal("DECISIONS ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDecision(2))));
        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.Equal("HANDOFF THREE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(3))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal(2, orchestrator.IterationCounter);
    }

    [Fact]
    public async Task The_conversation_projection_records_the_loop_transcript()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        IReadOnlyList<ConversationEntry> entries = orchestrator.Conversation.Entries;
        // The transcript is CAUSALLY ordered: submit -> continuation -> the next decision proposal.
        int submitIndex = IndexOfKind(entries, ConversationEntryKind.Submit);
        int continuationIndex = IndexOfKind(entries, ConversationEntryKind.Continuation);
        int decisionIndex = IndexOfKind(entries, ConversationEntryKind.DecisionOutput);
        Assert.True(submitIndex >= 0 && continuationIndex >= 0 && decisionIndex >= 0);
        Assert.True(submitIndex < continuationIndex, "submit must precede continuation");
        Assert.True(continuationIndex < decisionIndex, "continuation must precede the next decision proposal");
        // All three entries belong to the first decision/continuation iteration (the iteration counter advanced once).
        Assert.All(entries, e => Assert.Equal(1, e.Iteration));
        // Strictly increasing, dense sequence numbers (an ordered transcript, not a bag).
        Assert.Equal(
            Enumerable.Range(1, entries.Count).ToArray(),
            entries.Select(e => e.Sequence).ToArray());
    }

    [Fact]
    public async Task The_conversation_projection_records_planning_then_operational_output_in_order()
    {
        // Covers the two loop-prologue kinds the submit-only test cannot reach: a Planning entry from the
        // held-open authoring turn, then an OperationalOutput entry from Execute Plan's start-execution handoff.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        await WritePlanAsync(orchestrator, runtime, store, repository);
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, FirstHandoff);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        IReadOnlyList<ConversationEntry> entries = orchestrator.Conversation.Entries;
        Assert.Equal(ConversationEntryKind.Planning, entries[0].Kind);
        Assert.Equal(OrchestrationArtifactPaths.Plan, entries[0].Reference);
        Assert.Equal(ConversationEntryKind.OperationalOutput, entries[1].Kind);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), entries[1].Reference);
    }

    [Fact]
    public async Task A_failed_continuation_turn_does_not_rotate_a_handoff_or_route_the_next_decision_run()
    {
        // The `continued` guard is the m6 mechanism that suppresses rotation AND the next decision run when a
        // continuation fails — otherwise the UI would loop over a broken handoff. Lock that negative invariant.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "continue boom"));

        List<OrchestratorStreamEvent> events = await SubmitAndDrainExecutionAsync(orchestrator, repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;

        // The stream reported the failure on the ContinueExecution phase.
        OrchestratorStreamEvent failed = events.Single(e => e.Type == "failed");
        Assert.Equal("ContinueExecution", Field(failed, "phase"));
        Assert.Equal("The continue execution run failed.", Field(failed, "reason"));
        // No handoff was rotated; no rotation event was emitted.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.DoesNotContain(events, e => e.Type == "handoff-rotated");
        // The execution gate was released, so the repository is not wedged.
        Assert.False(orchestrator.IsExecutionRunActive);
        // The next decision run was NOT auto-started: no decisionRun was launched and no Decision session opened.
        Assert.Same(Task.CompletedTask, orchestrator.DecisionRunTask);
        Assert.DoesNotContain(runtime.Sessions, s => s.Role == SessionRole.Decision);
    }

    [Fact]
    public async Task Continuation_publishes_failed_when_no_handoff_is_available_to_continue_from()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        // No handoff of any kind exists.

        List<OrchestratorStreamEvent> events = await SubmitAndDrainExecutionAsync(orchestrator, repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;

        OrchestratorStreamEvent failed = events.Single(e => e.Type == "failed");
        Assert.Equal("ContinueExecution", Field(failed, "phase"));
        Assert.Contains("No execution handoff is available to continue from", Field(failed, "reason"));
        Assert.Empty(runtime.OneShotPrompts); // the continuation turn never ran
        Assert.DoesNotContain(events, e => e.Type == "handoff-rotated");
        Assert.Same(Task.CompletedTask, orchestrator.DecisionRunTask);
    }

    [Fact]
    public async Task Continuation_publishes_failed_when_the_handoff_is_not_written()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        // The continuation turn COMPLETES but writes no live handoff (no Effect).
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn());

        List<OrchestratorStreamEvent> events = await SubmitAndDrainExecutionAsync(orchestrator, repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;

        OrchestratorStreamEvent failed = events.Single(e => e.Type == "failed");
        Assert.Equal("ContinueExecution", Field(failed, "phase"));
        Assert.Equal("Continue execution completed but .agents/handoffs/handoff.md was not written.", Field(failed, "reason"));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.DoesNotContain(events, e => e.Type == "handoff-rotated");
        Assert.Same(Task.CompletedTask, orchestrator.DecisionRunTask);
    }

    [Fact]
    public async Task Dispose_drains_an_in_flight_continuation_before_completing_the_streams()
    {
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
        // The continuation one-shot signals once it is genuinely in flight, then parks — so the dispose drain
        // is observed against a continuation that has actually started, not merely the synchronous gate claim.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
        {
            parked.TrySetResult();
            return release.Task;
        }));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await parked.Task;
        Assert.True(orchestrator.IsExecutionRunActive);

        Task dispose = orchestrator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(dispose.IsCompleted); // parked: Dispose is draining the in-flight continuation

        release.SetResult();
        await dispose; // completes cleanly — no publish-after-complete throw, no orphaned next-decision launch

        Assert.True(orchestrator.IsDisposed);
        Assert.True(orchestrator.ExecutionStream.IsCompleted);
        Assert.True(orchestrator.DecisionStream.IsCompleted);
        // A continuation cancelled mid-turn must NOT have routed the next decision run.
        Assert.DoesNotContain(runtime.Sessions, s => s.Role == SessionRole.Decision);
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static int IndexOfKind(IReadOnlyList<ConversationEntry> entries, ConversationEntryKind kind)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

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

    private static async Task<List<OrchestratorStreamEvent>> SubmitAndDrainExecutionAsync(
        RepositoryOrchestrator orchestrator,
        Repository repository,
        string decisions)
    {
        Task<List<OrchestratorStreamEvent>> drain = DrainUntilAsync(orchestrator.ExecutionStream, "completed", "failed");
        await orchestrator.BeginSubmitDecisionsAsync(repository, decisions);
        return await drain;
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

    private static int IntField(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetInt32();
}

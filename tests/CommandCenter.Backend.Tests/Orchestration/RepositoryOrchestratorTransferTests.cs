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
/// Router Reuse and Transfer (m7) at the orchestrator level. After a continuation, the loop routes the next
/// decision turn on decision-session token pressure. A <see cref="DecisionRoute.Transfer"/> recycles the warm
/// Decision process — extract an operational delta, rewrite operational context, close the old process, seed a
/// FRESH one from the rewritten context — then proposes against it. Transfer is gated by eligibility (a primed
/// process must exist) and by the execution gate (its operational rewrite is mutually exclusive with a
/// continuation). These cover the real-router threshold path, the full transfer sequence + provenance + stream,
/// all three failure windows with process cleanup, the eligibility gate, router-fault degradation, the
/// execution-gate exclusion, and that the loop continues through reuse THEN transfer.
/// </summary>
public sealed class RepositoryOrchestratorTransferTests
{
    private const string Plan = "PLAN TEXT";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    [Fact]
    public async Task Transfer_extracts_a_delta_rewrites_context_and_recycles_the_decision_process()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // a prior proposal primes the warm process (transfer eligibility)
        ScriptTransferTurns(runtime, store, repository, delta: "OPERATIONAL DELTA", rewrittenContext: "REWRITTEN CONTEXT", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The delta the warm process produced was persisted, and the context was rewritten from it.
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("REWRITTEN CONTEXT", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Contains(UpdateOperationalContext.Text, runtime.OneShotPrompts);

        // The old Decision process was closed and a fresh one opened: two Decision sessions, the first disposed.
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);
        Assert.True(decisionSessions[0].Disposed);
        Assert.False(decisionSessions[1].Disposed);

        // The fresh process was seeded FROM TRANSFER (rewritten context), then proposed over the rotated handoff.
        Assert.Equal(StartDecisionSessionFromTransfer.Render("REWRITTEN CONTEXT"), decisionSessions[1].Prompts[0]);
        Assert.Equal(GetNextDecisions.Render("HANDOFF TWO"), decisionSessions[1].Prompts[1]);
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
        Assert.True(router.EvaluateCount >= 1);
    }

    [Fact]
    public async Task The_real_router_transfers_when_decision_pressure_crosses_the_threshold()
    {
        // The CRITICAL end-to-end check: with the REAL registry-free router (no fake), a decision session whose
        // observed token pressure crosses a low threshold actually routes Transfer. This is the path the
        // production DI composes; it must be reachable, which a fake-router test can never prove.
        var runtime = new FakeAgentRuntime { TurnUsage = new AgentTokenUsage(40, 60) }; // 100 observed tokens per proposal
        var store = new FakeArtifactStore();
        // Threshold above the deterministic estimate (~7 from the handoff + decisions text) but below the observed
        // pressure (100), so the Transfer verdict is driven SPECIFICALLY by observed accounting, not the fallback.
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(DecisionTokenTransferThreshold: 50));
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // accumulates ~100 observed decision-session tokens
        ScriptTransferTurns(runtime, store, repository, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The real router elected Transfer purely from observed decision-session pressure — the delta was written.
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task Observed_decision_tokens_are_surfaced_through_router_inputs()
    {
        var runtime = new FakeAgentRuntime { TurnUsage = new AgentTokenUsage(40, 60) };
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter(); // Continue
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT")); // the reuse proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The router was fed a non-empty observed signal (the seed proposal's tokens flowed into RouterInputs).
        Assert.True(orchestrator.RouterInputs.DecisionSessionTokens > 0);
        Assert.True(router.EvaluatedInputs.Count > 0);
        Assert.True(router.EvaluatedInputs[0].DecisionSessionTokens > 0);
    }

    [Fact]
    public async Task Transfer_records_provenance_for_each_transfer_prompt()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        PromptProvenance delta = Single(orchestrator.DecisionProvenance, nameof(ProduceOperationalDelta));
        Assert.Equal(ProduceOperationalDelta.SourceHash, delta.SourceHash);
        Assert.Equal(PromptSessionRole.Transfer, delta.SessionRole);
        Assert.Empty(delta.InputArtifactIdentities); // renders no files — extracts from the conversation
        Assert.Equal(OrchestrationArtifactPaths.OperationalDelta, Assert.Single(delta.OutputArtifactIdentities));

        PromptProvenance rewrite = Single(orchestrator.DecisionProvenance, nameof(UpdateOperationalContext));
        Assert.Equal(UpdateOperationalContext.SourceHash, rewrite.SourceHash);
        Assert.Equal(PromptSessionRole.ContextUpdate, rewrite.SessionRole);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.OperationalContext, OrchestrationArtifactPaths.OperationalDelta },
            rewrite.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(rewrite.OutputArtifactIdentities));

        PromptProvenance reseed = Single(orchestrator.DecisionProvenance, nameof(StartDecisionSessionFromTransfer));
        Assert.Equal(StartDecisionSessionFromTransfer.SourceHash, reseed.SourceHash);
        Assert.Equal(PromptSessionRole.Transfer, reseed.SessionRole);

        // The transfer-triggered proposal (the LAST GetNextDecisions, after the seed run's) is recorded too.
        PromptProvenance proposal = orchestrator.DecisionProvenance.Last(p => p.PromptName == nameof(GetNextDecisions));
        Assert.Equal(PromptSessionRole.Decision, proposal.SessionRole);
    }

    [Fact]
    public async Task Transfer_streams_its_phases_and_a_transferred_marker_then_review_ready()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Drain the seed run's review-ready AND the transfer run's terminal; the transfer events are the tail.
        List<OrchestratorStreamEvent> all = await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2);
        int firstReview = all.FindIndex(e => e.Type == "review-ready");
        List<OrchestratorStreamEvent> transferEvents = all.Skip(firstReview + 1).ToList();

        List<string?> phases = transferEvents.Where(e => e.Type == "phase").Select(e => Field(e, "phase")).ToList();
        Assert.Equal(
            new[] { "ProduceOperationalDelta", "UpdateOperationalContext", "StartDecisionSessionFromTransfer", "GetNextDecisions" },
            phases);
        Assert.Contains(transferEvents, e => e.Type == "transferred");
        Assert.Equal("review-ready", transferEvents[^1].Type);
        Assert.Equal("NEXT DECISIONS", Field(transferEvents[^1], "decisions"));
    }

    [Fact]
    public async Task Transfer_holds_the_execution_gate_and_blocks_a_concurrent_submit_while_rewriting()
    {
        // The transfer's context rewrite is an operational WORKSPACE WRITE; it must be mutually exclusive with a
        // continuation. Lock that the transfer holds the execution gate across the rewrite (so a concurrent
        // submit is rejected), closing the concurrent-workspace-write race.
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        // The rewrite one-shot signals it is parked (transfer holds the execution gate), then waits for release.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            parked.TrySetResult();
            await release.Task;
            await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), "CTX2");
        }));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await parked.Task; // transfer is mid-rewrite, holding the execution gate

        Assert.True(orchestrator.IsExecutionRunActive);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.BeginSubmitDecisionsAsync(repository, "RACE"));

        release.SetResult();
        await orchestrator.DecisionRunTask;

        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("NEXT", orchestrator.CurrentDecisions);
        Assert.False(orchestrator.IsExecutionRunActive); // gate released after the transfer
    }

    [Fact]
    public async Task Transfer_is_skipped_when_the_warm_process_is_not_seeded()
    {
        // Eligibility: a Transfer verdict on an UNSEEDED process must degrade to warm reuse (which then seeds),
        // never extract a bogus delta from an empty conversation.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository); // NB: no prior decision run, so decisionSeeded is false
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // StartDecisionSession seed (reuse path)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS")); // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The ineligible Transfer became a Continue: no delta, no context rewrite, a single (still-open) process
        // seeded the normal way and proposed.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.DoesNotContain(UpdateOperationalContext.Text, runtime.OneShotPrompts);
        FakeAgentSession decision = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.False(decision.Disposed);
        Assert.Equal(StartDecisionSession.Render(OperationalContext), decision.Prompts[0]);
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task A_router_fault_degrades_to_warm_reuse()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Throw = true };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS")); // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The router threw, the loop degraded to Continue, and a normal decision run still happened (no transfer).
        Assert.True(router.EvaluateCount >= 1);
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task Transfer_failed_delta_extraction_leaves_the_warm_process_usable_and_does_not_rewrite()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "delta boom")); // delta extraction fails

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent failed = (await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2))[^1];
        Assert.Equal("failed", failed.Type);
        Assert.Equal("ProduceOperationalDelta", Field(failed, "phase"));
        // Nothing downstream: no delta, context untouched, no rewrite one-shot, decisions unchanged.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal(OperationalContext, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.DoesNotContain(UpdateOperationalContext.Text, runtime.OneShotPrompts);
        Assert.Equal("DECISIONS ONE", orchestrator.CurrentDecisions);
        // The warm process was NOT recycled — it is still the single, USABLE (non-disposed) Decision process.
        FakeAgentSession warm = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.False(warm.Disposed);
        Assert.False(orchestrator.IsDecisionRunActive);
    }

    [Fact]
    public async Task Transfer_failed_context_rewrite_closes_the_old_process_and_does_not_propose()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "rewrite boom")); // rewrite fails (after close)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent failed = (await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2))[^1];
        Assert.Equal("failed", failed.Type);
        Assert.Equal("UpdateOperationalContext", Field(failed, "phase"));
        // The delta succeeded and was persisted; the rewrite failed, so no fresh process and no proposal.
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("DECISIONS ONE", orchestrator.CurrentDecisions);
        // Process cleanup: the old process was closed and NO orphaned fresh process was left open.
        FakeAgentSession closed = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.True(closed.Disposed);
        Assert.False(orchestrator.IsDecisionRunActive);
    }

    [Fact]
    public async Task Transfer_failed_reseed_tears_down_the_fresh_process_and_does_not_propose()
    {
        // The riskiest window: the reseed turn fails AFTER a fresh process was already opened. It must be torn
        // down (process cleanup), leaving no orphaned, half-seeded process.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, "CTX2"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));                  // delta (warm process)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "reseed boom")); // reseed (fresh process) fails

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent failed = (await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2))[^1];
        Assert.Equal("failed", failed.Type);
        Assert.Equal("StartDecisionSessionFromTransfer", Field(failed, "phase"));
        // Both processes (old warm + half-seeded fresh) were torn down; no proposal ran.
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);
        Assert.All(decisionSessions, s => Assert.True(s.Disposed));
        Assert.Equal("DECISIONS ONE", orchestrator.CurrentDecisions);
        Assert.False(orchestrator.IsDecisionRunActive);
    }

    [Fact]
    public async Task The_loop_continues_through_reuse_then_transfer()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter();
        router.Routes.Enqueue(DecisionRoute.Continue); // iteration 1 reuses (and seeds) the warm process
        router.Routes.Enqueue(DecisionRoute.Transfer); // iteration 2 transfers
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        // One-shots: iter1 continuation, iter2 continuation, iter2 context rewrite.
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF THREE"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, "CONTEXT TWO"));
        // Session turns: iter1 seed + proposal; iter2 delta + reseed + proposal.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // iter1 StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS A"));     // iter1 proposal
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA TWO"));       // iter2 delta extraction
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // iter2 reseed-from-transfer
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DECISIONS B"));     // iter2 proposal

        // Iteration 1 (Continue / warm reuse, which also seeds the process).
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;
        Assert.Equal("DECISIONS A", orchestrator.CurrentDecisions);

        // Iteration 2 (Transfer) on the SAME orchestrator — the loop survives the recycle.
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Equal("HANDOFF TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(2))));
        Assert.Equal("HANDOFF THREE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(3))));
        Assert.Equal("DELTA TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("DECISIONS B", orchestrator.CurrentDecisions);
        Assert.Equal(2, orchestrator.IterationCounter);

        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);
        Assert.True(decisionSessions[0].Disposed);  // the iteration-1 warm process, recycled by the transfer
        Assert.False(decisionSessions[1].Disposed); // the fresh post-transfer process
    }

    [Fact]
    public async Task Dispose_drains_an_in_flight_transfer_before_completing_the_streams()
    {
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        // The delta turn signals it is in flight, then parks — disposal races the transfer mid-sequence.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
        {
            parked.TrySetResult();
            return release.Task;
        }));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await parked.Task;

        Task dispose = orchestrator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(dispose.IsCompleted); // draining the in-flight transfer

        release.SetResult();
        await dispose; // completes cleanly

        Assert.True(orchestrator.IsDisposed);
        Assert.True(orchestrator.DecisionStream.IsCompleted);
        Assert.True(orchestrator.ExecutionStream.IsCompleted);
    }

    [Fact]
    public async Task Transfer_deferred_when_the_execution_gate_is_held_falls_through_to_warm_reuse()
    {
        // A Transfer verdict that cannot claim the execution gate (a continuation holds it) DEFERS to warm reuse
        // this round — no recycle — and the run announces the EFFECTIVE route "Continue", not the verdict.
        var continuationGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // warm process is seeded (transfer-eligible)

        // Park a continuation so it holds the execution gate (runState=Executing) while a Transfer decision run routes.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
        {
            parked.TrySetResult();
            return continuationGate.Task; // never writes a handoff -> the continuation simply holds the gate
        }));
        Task submit = orchestrator.BeginSubmitDecisionsAsync(repository, "HOLD");
        await parked.Task;
        Assert.True(orchestrator.IsExecutionRunActive);

        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "REUSE DECISIONS")); // the reuse proposal (no re-seed)
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Transfer);
        await orchestrator.DecisionRunTask;

        // The Transfer was deferred: no delta, the warm process was NOT recycled, and it proposed via reuse.
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        FakeAgentSession warm = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.False(warm.Disposed);
        Assert.Equal("REUSE DECISIONS", orchestrator.CurrentDecisions);
        // The run announced the EFFECTIVE route — Continue, not the deferred Transfer verdict.
        List<OrchestratorStreamEvent> events = await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2);
        Assert.Equal("Continue", Field(events.Last(e => e.Type == "run-started"), "route"));

        continuationGate.SetResult();
        await submit;
        await orchestrator.ExecutionRunTask;
    }

    [Fact]
    public async Task The_router_signal_falls_back_to_a_deterministic_estimate_before_any_decision_turn()
    {
        // Before any decision proposal has been observed (decisionSessionTokens == 0), the router is fed a
        // DETERMINISTIC estimate from the content the session reasons over (latest handoff + decisions), not zero.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter(); // Continue; records the inputs it was fed
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository); // NB: no prior decision run -> zero observed tokens
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                   // seed (reuse path)
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "PROPOSAL")); // proposal

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The router's first evaluation (at routing, before the decision run accumulated any tokens) used the
        // deterministic estimate of the rotated handoff + submitted decisions — a positive, content-derived signal.
        int estimate = Estimate("HANDOFF TWO") + Estimate("DECISIONS ONE");
        Assert.True(estimate > 0);
        Assert.Equal(estimate, router.EvaluatedInputs[0].DecisionSessionTokens);
    }

    [Fact]
    public async Task The_token_pressure_resets_on_recycle_so_the_next_iteration_reuses_the_fresh_process()
    {
        // Recycling a process MUST reset its observed pressure to 0; otherwise the fresh process inherits the old
        // pressure and immediately re-transfers. Drive a real-router transfer, then a LOW-pressure follow-up that
        // must route Continue (reuse) — which only holds if the reset fired.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(DecisionTokenTransferThreshold: 50));
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        // Seed with a HIGH-pressure proposal (100 tokens) so iteration 1 crosses the threshold and transfers.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                                                   // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED", Usage: new AgentTokenUsage(50, 50))); // 100 tokens
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        // Iteration 1: transfers (observed 100 >= 50). Its post-transfer proposal is LOW pressure (10 tokens).
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, "CTX2"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));                                    // delta
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                                                   // reseed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "AFTER1", Usage: new AgentTokenUsage(5, 5))); // 10 tokens
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Iteration 2: with the reset, observed pressure is now 10 (< 50) -> Continue (reuse the fresh process).
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF THREE"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "AFTER2"));                                   // reuse proposal
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Exactly ONE recycle happened (iteration 1). Iteration 2 REUSED the fresh process — not recycled again —
        // which is only true because the recycle reset the pressure below the threshold (a non-reset would re-transfer).
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);
        Assert.True(decisionSessions[0].Disposed);
        Assert.False(decisionSessions[1].Disposed);
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("AFTER2", orchestrator.CurrentDecisions);
    }

    // ---- helpers ----

    // The deterministic token estimate the orchestrator uses for the router's fallback signal ((len+3)/4).
    private static int Estimate(string text) => (text.Length + 3) / 4;

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    // The minimal pre-state a continuation needs: a cached plan, an operational context (the decision gate
    // requires it), and a first rotated handoff for the continuation to build on.
    private static async Task SeedLoopAsync(RepositoryOrchestrator orchestrator, FakeArtifactStore store, Repository repository)
    {
        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
    }

    // A prior Continue decision run primes the warm process (decisionSeeded == true) so a later Transfer is
    // eligible, and accumulates observed decision-session tokens.
    private static async Task SeedWarmDecisionSessionAsync(RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, Repository repository)
    {
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS"));  // initial proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;
    }

    // Scripts the two one-shots (continuation, then context rewrite) and three decision-session turns (delta,
    // reseed, proposal) a happy-path transfer consumes after a submit.
    private static void ScriptTransferTurns(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        string delta,
        string rewrittenContext,
        string proposal)
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

    private static PromptProvenance Single(IReadOnlyList<PromptProvenance> provenance, string promptName) =>
        provenance.Single(p => p.PromptName == promptName);

    // Drains until the Nth terminal (review-ready or failed) so a test can reach a transfer run's terminal that
    // follows the seed run's review-ready.
    private static async Task<List<OrchestratorStreamEvent>> DrainDecisionTerminalsAsync(OrchestratorStreamChannel stream, int terminalCount)
    {
        var events = new List<OrchestratorStreamEvent>();
        int seen = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (OrchestratorStreamEvent streamEvent in stream.SubscribeAsync(0, cts.Token))
        {
            events.Add(streamEvent);
            if (streamEvent.Type is "review-ready" or "failed" && ++seen >= terminalCount)
            {
                break;
            }
        }

        return events;
    }

    private static string? Field(OrchestratorStreamEvent streamEvent, string property) =>
        JsonDocument.Parse(streamEvent.Data).RootElement.GetProperty(property).GetString();
}

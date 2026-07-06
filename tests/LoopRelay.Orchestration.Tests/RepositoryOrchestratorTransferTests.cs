using System.Linq;
using System.Text.Json;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Streaming;

namespace LoopRelay.Orchestration.Tests;

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
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // a prior proposal primes the warm process (transfer eligibility)
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "OPERATIONAL DELTA", rewrittenContext: "REWRITTEN CONTEXT", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The delta the warm process produced was persisted, and the context was rewritten from it.
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
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
    public async Task The_real_router_transfers_when_occupancy_crosses_the_capacity_guard()
    {
        // The CRITICAL end-to-end check: with the REAL registry-free router (no fake), a decision process whose
        // occupancy crosses the capacity guard actually routes Transfer. This is the path production DI composes; it
        // must be reachable, which a fake-router test can never prove. (The economic marginal rule is unit-tested in
        // DecisionSessionRouterTests; the FIRST transfer is capacity-driven because C starts at the 250k seed.)
        var runtime = new FakeAgentRuntime { TurnUsage = new AgentTokenUsage(40, 60) }; // 100 occupancy per proposal
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        // Window 110 -> capacity guard round(110*0.9)=99; the seed proposal's occupancy (100) crosses it.
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 110, CapacityGuardFraction: 0.90));
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // one proposal -> occupancy 100 (>= guard 99)
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The real router elected Transfer from the capacity guard — the delta was written and context rewritten.
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);

        // The transfer's MEASURED cost recalibrated the estimate off the 250k seed (cost-aware self-calibration).
        Assert.NotEqual(250_000d, orchestrator.RouterInputs.TransferCostEstimate);
        Assert.True(orchestrator.RouterInputs.TransferCostEstimate > 0d);
    }

    [Fact]
    public async Task The_real_router_transfers_economically_below_the_capacity_guard()
    {
        // Proves the ECONOMIC marginal path end-to-end: occupancy stays FAR below the capacity guard, yet the cost
        // model drives eNext >= (R + C)/n so the REAL router elects Transfer purely on economics. A controllable cost
        // model isolates this (with the real model the first transfer is capacity-driven while C is at the 250k seed).
        // A wiring bug that dropped eNext (e.g. never calling EstimateNextCycle) would fail THIS test specifically.
        var runtime = new FakeAgentRuntime { TurnUsage = new AgentTokenUsage(50, 50) }; // occupancy 100 << guard 230,400
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var costModel = new FakeDecisionCostModel { MeasureValue = 300_000d, EstimateValue = 600_000d };
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions()); // default: 256k window, marginal policy
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, costModel: costModel, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // one proposal -> R=300000, n=1, occupancy 100
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Occupancy (100) is far below the capacity guard (230,400): this Transfer was driven ONLY by the marginal
        // rule — eNext (600,000) >= (R 300,000 + C 250,000 seed) / n 1 = 550,000.
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("NEXT DECISIONS", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task Transfer_cost_estimate_is_a_running_average_of_measured_transfers()
    {
        // Two forced transfers with DIFFERENT measured costs: C must be the running average, not last-value-wins.
        // Measured cost per transfer = 3 turns (delta+rewrite+reseed) * MeasureValue.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var costModel = new FakeDecisionCostModel { MeasureValue = 100d, EstimateValue = 0d };
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer }; // force both transfers
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, costModel: costModel, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // primes the process (transfer eligible)

        // Transfer 1: measured cost = 3 * 100 = 300 -> C = 300 (first measured replaces the 250k seed).
        costModel.MeasureValue = 100d;
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA1", rewrittenContext: "CTX1", proposal: "P1");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;
        Assert.Equal(300d, orchestrator.RouterInputs.TransferCostEstimate, 4);

        // Transfer 2: measured cost = 3 * 200 = 600 -> running average C = 300 + (600 - 300)/2 = 450 (NOT 600).
        costModel.MeasureValue = 200d;
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA2", rewrittenContext: "CTX2", proposal: "P2");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;
        Assert.Equal(450d, orchestrator.RouterInputs.TransferCostEstimate, 4);
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

        // The cost-aware signals flow to the router: after the seed proposal + one reuse proposal (cost 100 each via
        // the default model), occupancy is the LATEST (100), R is CUMULATIVE (200), and n counts both cycles (2).
        Assert.Equal(100, orchestrator.RouterInputs.OccupancyTokens);
        Assert.Equal(200d, orchestrator.RouterInputs.AccumulatedReuseCost);
        Assert.Equal(2, orchestrator.RouterInputs.ReuseCycleCount);
        // The router's FIRST evaluation already saw the seed proposal's cost-aware fields (n=1, R=100).
        Assert.True(router.EvaluatedInputs.Count > 0);
        Assert.Equal(1, router.EvaluatedInputs[0].ReuseCycleCount);
        Assert.Equal(100d, router.EvaluatedInputs[0].AccumulatedReuseCost);
    }

    [Fact]
    public async Task Decision_pressure_reflects_the_latest_proposal_occupancy_not_the_cumulative_sum()
    {
        // The router's signal is the live decision process's CURRENT context occupancy (the latest turn's
        // prompt+output = last_token_usage.input_tokens), NOT the sum of every proposal's billing. A warm
        // process re-sends its whole conversation each turn, so summing per-turn billing over-counts the real
        // context ~quadratically; the occupancy is what actually decides whether the window is near full.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter(); // Continue (reuse the warm process across both proposals)
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);

        // First decision run: seed + a HIGH-occupancy proposal (100 tokens).
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                                                  // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "P1", Usage: new AgentTokenUsage(60, 40)));  // 100 occupancy
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;
        Assert.Equal(100, orchestrator.RouterInputs.OccupancyTokens); // occupancy after the first proposal

        // Second proposal on the SAME warm process: LOWER occupancy (10 tokens) — the context shrank.
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "P2", Usage: new AgentTokenUsage(5, 5)));    // 10 occupancy
        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // OCCUPANCY is the LATEST proposal's size (10), NOT a running sum — that's the capacity signal. The
        // ACCUMULATED REUSE COST (R) is the separate, cumulative signal the marginal rule amortizes: cost(P1)=100
        // (60 fresh + 40 out) + cost(P2)=10 (5 fresh + 5 out) = 110, over n=2 cycles.
        Assert.Equal(10, orchestrator.RouterInputs.OccupancyTokens);
        Assert.Equal(110d, orchestrator.RouterInputs.AccumulatedReuseCost);
        Assert.Equal(2, orchestrator.RouterInputs.ReuseCycleCount);
    }

    [Fact]
    public async Task Transfer_records_provenance_for_each_transfer_prompt()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT");

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
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT DECISIONS");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Drain the seed run's review-ready AND the transfer run's terminal; the transfer events are the tail.
        List<OrchestratorStreamEvent> all = await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2);
        int firstReview = all.FindIndex(e => e.Type == "review-ready");
        List<OrchestratorStreamEvent> transferEvents = all.Skip(firstReview + 1).ToList();

        List<string?> phases = transferEvents.Where(e => e.Type == "phase").Select(e => Field(e, "phase")).ToList();
        Assert.Equal(
            new[] { "ProduceOperationalDelta", "UpdateOperationalContext", "ArchiveOperationalDelta", "StartDecisionSessionFromTransfer", "GetNextDecisions" },
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
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        // The rewrite one-shot signals it is parked (transfer holds the execution gate), then waits for release.
        // It writes the evolved context into the SANDBOX workspace (Stage 2), where the orchestrator reads it back.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            parked.TrySetResult();
            await release.Task;
            await store.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "CTX2");
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
        Assert.Equal("DELTA TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
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
        // Before any reuse cycle has completed (n == 0), the router's occupancy signal is a DETERMINISTIC estimate
        // from the content the session reasons over (latest handoff + decisions), not zero.
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
        Assert.Equal(estimate, router.EvaluatedInputs[0].OccupancyTokens);
    }

    [Fact]
    public async Task Recycle_resets_the_per_process_cost_accounting_so_the_fresh_process_reuses()
    {
        // A recycle MUST reset the per-process cost accounting (R, n, occupancy); otherwise the fresh process
        // inherits the old run's cost and could immediately re-transfer. Drive a capacity-guard transfer, verify the
        // fresh process's accounting reflects ONLY its own post-transfer cycle, and confirm the next iteration reuses.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        // Window 110 -> guard round(110*0.9)=99; the seed proposal (occupancy 100) crosses it so iteration 1 transfers.
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 110, CapacityGuardFraction: 0.90));
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        // Seed with a HIGH-occupancy proposal (100) so iteration 1 crosses the guard and transfers.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                                                   // seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED", Usage: new AgentTokenUsage(50, 50))); // occupancy 100
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        // Iteration 1: transfers (occupancy 100 >= guard 99). Its post-transfer proposal is LOW occupancy (10).
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, "CTX2"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));                                    // delta
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                                                   // reseed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "AFTER1", Usage: new AgentTokenUsage(5, 5))); // occupancy 10
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // RESET PROOF: the fresh process's accounting reflects ONLY its single post-transfer proposal — n == 1 and
        // R == cost(AFTER1) == 10. Without the recycle reset the pre-transfer seed cycle would still count (n == 2,
        // R == 110), so this assertion is what actually pins ResetDecisionProcessAccounting.
        Assert.Equal(1, orchestrator.RouterInputs.ReuseCycleCount);
        Assert.Equal(10d, orchestrator.RouterInputs.AccumulatedReuseCost);
        Assert.Equal(10, orchestrator.RouterInputs.OccupancyTokens);

        // Iteration 2: occupancy 10 < guard, and the marginal rule keeps the cheap fresh process -> Continue (reuse).
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF THREE"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "AFTER2"));                                   // reuse proposal
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // Exactly ONE recycle happened (iteration 1). Iteration 2 REUSED the fresh process — not recycled again.
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(2, decisionSessions.Count);
        Assert.True(decisionSessions[0].Disposed);
        Assert.False(decisionSessions[1].Disposed);
        Assert.Equal("DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("AFTER2", orchestrator.CurrentDecisions);
    }

    // ---- Stage 2: sandboxed operational-context evolution + size health guard ----

    [Fact]
    public async Task Transfer_evolves_the_operational_context_in_an_isolated_sandbox_then_copies_it_back()
    {
        // Stage 2: the dominant transfer cost was the evolution one-shot re-exploring the whole repo. It now runs in
        // a temp workspace seeded with ONLY the current context + the delta (codex --cd scopes its sandbox there),
        // and the orchestrator copies the evolved context back into the repo.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "REWRITTEN", proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The evolution one-shot ran against the SANDBOX root, not the repository — and still workspace-write.
        // (The continuation is ALSO an OperationalExecution one-shot, so identify the evolution by its prompt.)
        Assert.Contains(UpdateOperationalContext.Text, runtime.OneShotPrompts);
        AgentSessionSpec evolutionSpec = runtime.OneShotSpecs[runtime.OneShotPrompts.IndexOf(UpdateOperationalContext.Text)];
        Assert.Equal(sandbox.Root, evolutionSpec.WorkingDirectory);
        Assert.NotEqual(repository.Path, evolutionSpec.WorkingDirectory);
        Assert.True(evolutionSpec.Sandbox.CanWriteWorkspace);

        // The sandbox was seeded with EXACTLY the two evolution inputs — the current context and the delta — no more.
        string sandboxContext = sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext);
        string sandboxDelta = sandbox.Resolve(OrchestrationArtifactPaths.OperationalDelta);
        List<string> sandboxWrites = store.WriteQueries
            .Where(p => p.StartsWith(sandbox.Root, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { sandboxContext, sandboxDelta }.OrderBy(p => p, StringComparer.Ordinal).ToList(), sandboxWrites);
        Assert.Equal("DELTA", await store.ReadAsync(sandboxDelta)); // the delta was copied into the sandbox for the rewrite
        // The sandbox was SEEDED with the CURRENT repo context (so codex folds the delta into the real base, not a
        // blank/wrong one). The one-shot later overwrites it, so assert the FIRST write to the sandbox context path.
        Assert.Equal(OperationalContext, store.Writes.First(w => w.Path == sandboxContext).Content);

        // The evolved context was copied back into the repo, and the fresh process was reseeded from it.
        Assert.Equal("REWRITTEN", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
        Assert.Equal(StartDecisionSessionFromTransfer.Render("REWRITTEN"), decisionSessions[^1].Prompts[0]);

        // The workspace was created once and disposed (cleaned up), even on the happy path.
        Assert.Equal(1, sandbox.CreatedCount);
        Assert.Single(sandbox.Disposed);
    }

    [Fact]
    public async Task Transfer_disposes_the_sandbox_even_when_the_rewrite_fails()
    {
        // The temp workspace must be cleaned up on the failure path too — a failed rewrite cannot leak a temp tree.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(AgentTurnState.Failed, Output: "rewrite boom")); // rewrite fails inside the sandbox
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // A sandbox was created for the rewrite and disposed despite the failure; the repo context is untouched.
        Assert.Equal(1, sandbox.CreatedCount);
        Assert.Single(sandbox.Disposed);
        Assert.Equal(OperationalContext, await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Transfer_off_switch_evolves_against_the_repository_working_directory()
    {
        // Rollback path: with the sandbox flag OFF the evolution runs against the repository cwd (pre-Stage-2
        // behavior) — the agent rewrites .agents/operational_context.md in place and no sandbox is created.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        var flags = new OrchestrationFeatureFlags(SandboxOperationalContextEvolutionEnabled: false);
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, flags: flags, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, "REWRITTEN")); // agent rewrites the REPO file in place
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Contains(UpdateOperationalContext.Text, runtime.OneShotPrompts);
        AgentSessionSpec evolutionSpec = runtime.OneShotSpecs[runtime.OneShotPrompts.IndexOf(UpdateOperationalContext.Text)];
        Assert.Equal(repository.Path, evolutionSpec.WorkingDirectory);
        Assert.Equal(0, sandbox.CreatedCount); // no sandbox on the OFF path
        Assert.Equal("REWRITTEN", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("NEXT", orchestrator.CurrentDecisions);
    }

    [Fact]
    public async Task Transfer_records_an_operational_context_size_health_baseline()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: new string('x', 42), proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The first transfer of a process's lifetime records a health BASELINE: the measured size, no warning.
        Assert.NotNull(orchestrator.LastOperationalContextHealth);
        OperationalContextHealth health = orchestrator.LastOperationalContextHealth!.Value;
        Assert.Equal(42, health.Size);
        Assert.Null(health.PreviousSize);
        Assert.Equal(0, health.GrowthStreak);
        Assert.False(health.Warning);
    }

    [Fact]
    public async Task Repeated_transfers_that_grow_the_operational_context_raise_a_ratchet_warning()
    {
        // The renewal-reward stability guard: a SUSTAINED upward ratchet across transfers warns. Baseline (no warn),
        // first growth (streak 1, no warn), second growth (streak 2 => WARN). Asserting after EACH transfer also
        // pins that the streak state PERSISTS across the recycle — reset it and the third transfer would never
        // reach the warning threshold.
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);

        await RunTransferAsync(orchestrator, runtime, store, sandbox, repository, "D1", new string('a', 100));
        Assert.NotNull(orchestrator.LastOperationalContextHealth);
        OperationalContextHealth first = orchestrator.LastOperationalContextHealth!.Value;
        Assert.Equal(100, first.Size);
        Assert.Equal(0, first.GrowthStreak); // baseline (no previous)
        Assert.False(first.Warning);

        await RunTransferAsync(orchestrator, runtime, store, sandbox, repository, "D2", new string('a', 200));
        OperationalContextHealth second = orchestrator.LastOperationalContextHealth!.Value;
        Assert.Equal(1, second.GrowthStreak); // first growth
        Assert.False(second.Warning);

        await RunTransferAsync(orchestrator, runtime, store, sandbox, repository, "D3", new string('a', 300));
        OperationalContextHealth third = orchestrator.LastOperationalContextHealth!.Value;
        Assert.Equal(300, third.Size);
        Assert.Equal(2, third.GrowthStreak); // sustained growth crosses the ratchet threshold
        Assert.True(third.Warning);
    }

    [Fact]
    public async Task Transfer_archives_the_operational_delta_after_the_context_update()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "OPERATIONAL DELTA", rewrittenContext: "REWRITTEN", proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The consumed delta was rotated into the numbered archive and the live file removed.
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
    }

    [Fact]
    public async Task Successive_transfers_archive_deltas_with_a_monotonic_sequence()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);

        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA ONE", rewrittenContext: "CTX1", proposal: "P1");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA TWO", rewrittenContext: "CTX2", proposal: "P2");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Equal("DELTA ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("DELTA TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(2))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
    }

    [Fact]
    public async Task Transfer_failed_delta_archive_fails_the_transfer_and_does_not_propose()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT");
        // Force the archive write into .agents/deltas to fail.
        store.FailWriteOn = path => path.Contains("deltas", StringComparison.OrdinalIgnoreCase);

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent failed = (await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2))[^1];
        Assert.Equal("failed", failed.Type);
        Assert.Equal("ArchiveOperationalDelta", Field(failed, "phase"));
        // The context update already succeeded, but the transfer failed at the archive: no fresh proposal.
        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("DECISIONS ONE", orchestrator.CurrentDecisions);
        Assert.False(orchestrator.IsDecisionRunActive);
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
    // reseed, proposal) a happy-path transfer consumes after a submit. The rewrite one-shot writes the evolved
    // context to the SANDBOX workspace (Stage 2) — the orchestrator reads it back from there and copies it into
    // the repo — so the effect targets the sandbox path, not the repo path.
    private static void ScriptTransferTurns(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        FakeSandboxWorkspaceFactory sandbox,
        string delta,
        string rewrittenContext,
        string proposal)
    {
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF TWO"));
        runtime.OneShotTurns.Enqueue(WritesSandboxOperationalContext(store, sandbox, rewrittenContext));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: delta));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: proposal));
    }

    // Drives ONE full transfer submit (continuation one-shot + sandbox rewrite one-shot + delta/reseed/proposal),
    // producing an operational context of the given size, and awaits it. The prior transfer leaves a seeded fresh
    // process, so successive calls remain transfer-eligible.
    private static async Task RunTransferAsync(
        RepositoryOrchestrator orchestrator,
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        FakeSandboxWorkspaceFactory sandbox,
        Repository repository,
        string decisionsInput,
        string rewrittenContext)
    {
        runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, "HANDOFF-" + decisionsInput));
        runtime.OneShotTurns.Enqueue(WritesSandboxOperationalContext(store, sandbox, rewrittenContext));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "DELTA-" + decisionsInput));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "PROPOSAL-" + decisionsInput));
        await orchestrator.BeginSubmitDecisionsAsync(repository, decisionsInput);
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;
    }

    private static FakeOneShotTurn WritesLiveHandoff(FakeArtifactStore store, Repository repository, string handoff) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff));

    private static FakeOneShotTurn WritesOperationalContext(FakeArtifactStore store, Repository repository, string context) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), context));

    // Stage 2: the evolution one-shot writes the rewritten context into the sandbox workspace (where codex --cd
    // is scoped), at the SAME absolute path the orchestrator reads back from.
    private static FakeOneShotTurn WritesSandboxOperationalContext(FakeArtifactStore store, FakeSandboxWorkspaceFactory sandbox, string context) =>
        new(Effect: () => store.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), context));

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

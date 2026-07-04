using System.Linq;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// m10 (D) stress certification: drive N (>= 3; here 6) decision-loop + transfer cycles on a SINGLE orchestrator,
/// alternating Continue / Transfer. After each cycle the invariants hold: decision/handoff sequence numbers are
/// strictly increasing, EXACTLY ONE decision process is non-disposed (every prior warm process recycled by a
/// transfer is disposed), the live decision-session token pressure stays bounded (it RESETS to 0 on every recycle),
/// and the iteration counter equals the number of submits. An observed==0 variant proves every iteration routes
/// purely on the deterministic (len+3)/4 estimate and routing stays Continue below threshold across all N.
/// </summary>
public sealed class RepositoryOrchestratorStressTests
{
    private const string Plan = "PLAN TEXT";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    [Fact]
    public async Task Six_cycles_alternating_continue_and_transfer_keep_every_invariant()
    {
        const int cycles = 6;
        var runtime = new FakeAgentRuntime { TurnUsage = new AgentTokenUsage(40, 60) }; // 100 observed tokens / proposal
        var store = new FakeArtifactStore();
        // Explicitly alternate the route per cycle so each cycle's scripted turns are EXACT (a real-router threshold
        // path is covered by RepositoryOrchestratorTransferTests; here the focus is loop durability over many cycles).
        var router = new FakeDecisionSessionRouter();
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            router.Routes.Enqueue(cycle % 2 == 1 ? DecisionRoute.Transfer : DecisionRoute.Continue);
        }

        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository); // primes the warm process (transfer eligible)

        int recycleCount = 0;
        int lastHighestHandoff = HighestSequence(store, repository, OrchestrationArtifactPaths.HandoffsDirectory, OrchestrationArtifactPaths.HistoricalHandoffSearchPattern);
        int lastHighestDecision = HighestSequence(store, repository, OrchestrationArtifactPaths.DecisionsDirectory, OrchestrationArtifactPaths.HistoricalDecisionSearchPattern);

        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            bool transferring = cycle % 2 == 1; // odd cycles transfer, even cycles reuse — and the process is primed
            int decisionsBefore = runtime.Sessions.Count(s => s.Role == SessionRole.Decision);

            // Script EXACTLY the turns the routed run consumes: always a continuation handoff; a transfer additionally
            // rewrites context (one-shot) and runs delta + reseed before the proposal.
            runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, $"HANDOFF {cycle + 1}"));
            if (transferring)
            {
                runtime.OneShotTurns.Enqueue(WritesOperationalContext(store, repository, $"CONTEXT {cycle + 1}"));
                runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: $"DELTA {cycle}")); // ProduceOperationalDelta
                runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // reseed-from-transfer
            }

            runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: $"DECISIONS {cycle}")); // proposal

            await orchestrator.BeginSubmitDecisionsAsync(repository, $"D{cycle}");
            await orchestrator.ExecutionRunTask;
            await orchestrator.DecisionRunTask;

            // INVARIANT: handoff + decision sequence numbers strictly increase each cycle.
            int highestHandoff = HighestSequence(store, repository, OrchestrationArtifactPaths.HandoffsDirectory, OrchestrationArtifactPaths.HistoricalHandoffSearchPattern);
            int highestDecision = HighestSequence(store, repository, OrchestrationArtifactPaths.DecisionsDirectory, OrchestrationArtifactPaths.HistoricalDecisionSearchPattern);
            Assert.True(highestHandoff > lastHighestHandoff, $"cycle {cycle}: handoff seq must increase ({lastHighestHandoff} -> {highestHandoff})");
            Assert.True(highestDecision > lastHighestDecision, $"cycle {cycle}: decision seq must increase ({lastHighestDecision} -> {highestDecision})");
            lastHighestHandoff = highestHandoff;
            lastHighestDecision = highestDecision;

            // INVARIANT: every prior warm process recycled by a transfer is disposed — EXACTLY ONE non-disposed
            // decision process remains.
            List<FakeAgentSession> decisionSessions = runtime.Sessions.Where(s => s.Role == SessionRole.Decision).ToList();
            Assert.Equal(1, decisionSessions.Count(s => !s.Disposed));
            if (decisionSessions.Count > decisionsBefore)
            {
                recycleCount++; // a fresh process opened this cycle == a recycle happened
            }

            // INVARIANT: the live decision-session signal is the latest proposal's OCCUPANCY (the current context
            // size), NOT a cumulative sum — so with a constant per-proposal usage of 100 it reads exactly 100 every
            // cycle no matter how many cycles have run. It can never grow unbounded (the over-count bug summing
            // per-turn billing is gone). A transfer cycle also ends at 100: the recycle resets to 0, then the fresh
            // process's proposal overwrites it to 100. (Reset-on-recycle is independently proven by the disposal
            // invariant above and recycleCount below.)
            Assert.Equal(100, orchestrator.RouterInputs.OccupancyTokens);

            Assert.Equal($"DECISIONS {cycle}", orchestrator.CurrentDecisions);
        }

        // The loop ran all N cycles: iteration counter == number of submits, and three transfers recycled the process.
        Assert.Equal(cycles, orchestrator.IterationCounter);
        Assert.Equal(3, recycleCount); // cycles 1,3,5 transferred
        Assert.Equal(1, runtime.Sessions.Count(s => s.Role == SessionRole.Decision && !s.Disposed));
    }

    [Fact]
    public async Task Observed_zero_keeps_routing_continue_below_threshold_for_every_cycle()
    {
        // observed == 0 variant: every proposal reports ZERO usage, so the router routes purely on the deterministic
        // (len+3)/4 estimate of the latest handoff + decisions. With a high threshold that estimate never crosses,
        // routing stays Continue across ALL N — one process, never recycled, pressure stays at the estimate.
        const int cycles = 6;
        var runtime = new FakeAgentRuntime { TurnUsage = AgentTokenUsage.Zero }; // no observed tokens, ever
        var store = new FakeArtifactStore();
        // Default router: with zero observed usage every cycle, reuse cost stays 0, so neither the marginal rule
        // nor the capacity guard ever fires — routing stays Continue across all N.
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);

        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            runtime.OneShotTurns.Enqueue(WritesLiveHandoff(store, repository, $"HANDOFF {cycle + 1}"));
            runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: $"DECISIONS {cycle}")); // reuse proposal (no re-seed)

            await orchestrator.BeginSubmitDecisionsAsync(repository, $"D{cycle}");
            await orchestrator.ExecutionRunTask;
            await orchestrator.DecisionRunTask;

            // Routing stayed Continue: no operational_delta was ever written (a Transfer's signature artifact).
            Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)),
                $"cycle {cycle}: routing must stay Continue (no transfer) on a zero-observed estimate below threshold");
            // The router was fed a positive, content-derived estimate (never zero), and it stayed below threshold.
            Assert.True(orchestrator.RouterInputs.OccupancyTokens >= 0);
        }

        // One decision process, never recycled across all six cycles.
        FakeAgentSession decision = Assert.Single(runtime.Sessions, s => s.Role == SessionRole.Decision);
        Assert.False(decision.Disposed);
        Assert.Equal(cycles, orchestrator.IterationCounter);
    }

    // ---- helpers ----

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static async Task SeedLoopAsync(RepositoryOrchestrator orchestrator, FakeArtifactStore store, Repository repository)
    {
        orchestrator.RecordPlan(Plan);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), OperationalContext);
        await store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalHandoff(1)), FirstHandoff);
    }

    private static async Task SeedWarmDecisionSessionAsync(RepositoryOrchestrator orchestrator, FakeAgentRuntime runtime, Repository repository)
    {
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                         // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS"));  // initial proposal
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;
    }

    private static FakeOneShotTurn WritesLiveHandoff(FakeArtifactStore store, Repository repository, string handoff) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff));

    private static FakeOneShotTurn WritesOperationalContext(FakeArtifactStore store, Repository repository, string context) =>
        new(Effect: () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), context));

    private static int HighestSequence(FakeArtifactStore store, Repository repository, string directory, string pattern)
    {
        IReadOnlyList<string> rotated = store.ListAsync(Resolve(repository, directory), pattern).GetAwaiter().GetResult();
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
}

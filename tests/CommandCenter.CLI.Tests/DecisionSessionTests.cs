using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class DecisionSessionTests
{
    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con)
        New(DecisionSessionRouterOptions? routerOptions = null, IDecisionCostModel? costModel = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(routerOptions ?? new DecisionSessionRouterOptions());
        return (new DecisionSession(rt, router, art, con, repo, costModel), rt, store, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_SeedsOnce_Proposes_PersistsAndVerifiesDecisions()
    {
        var (session, rt, store, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // seed
        {
            Assert.Contains("OPCTX", prompt);
            return Turns.Completed("seeded");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // propose
        {
            Assert.Contains("HANDOFF", prompt);
            return Turns.Completed("DECISIONS-TEXT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Contains("DECISIONS-TEXT", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
    }

    [Fact]
    public async Task Run_WhenOperationalContextEmpty_Warns()
    {
        var (session, rt, store, repo, con) = New();
        // Neither plan.md nor operational_context.md exists: EnsureOperationalContextAsync writes nothing,
        // so the operational-context read yields empty and the degraded condition must be surfaced via Warn.
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));   // seed
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));        // propose

        await session.RunAsync(CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task Run_SecondRound_ReusesWarmSession_NoReseed()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));  // no second seed

        await session.RunAsync(CancellationToken.None);
        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);     // warm reuse: only one process opened
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task Run_WhenProposeNotCompleted_ClosesSessionAndThrows()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task Dispose_ClosesWarmSession()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));

        await session.RunAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task Run_WhenOccupancyCrossesGuard_RecyclesViaTransfer()
    {
        // Small window (22 -> capacity guard round(22*0.9)=20): round 1 accrues occupancy 20, so round 2's routing
        // crosses the capacity guard and Transfers.
        var (session, rt, store, repo, con) = New(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: seed + propose (propose accrues tokens so round 2 routes Transfer).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer => produce-delta (warm) + close + update-context (one-shot) + reseed-from-transfer + propose.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) => Turns.Completed("DELTA-TEXT")));      // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                            // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated context");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                       // reseed from transfer
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("reseeded");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));                   // propose

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(2, rt.OpenSessions);   // original + recycled
        Assert.Equal(1, rt.ClosedSessions); // original closed during recycle
    }

    [Fact]
    public async Task Run_EconomicMarginalRule_RecyclesViaTransfer()
    {
        // Economic transfer BELOW the capacity guard: a controllable cost model makes eNext >= (R+C)/n while
        // occupancy stays tiny. Proves the CLI's BuildRouterInputs/RecordProposalCost economic wiring (a hand
        // mirror of the orchestrator) actually produces a Transfer — not just the capacity-guard path.
        var costModel = new StubCostModel { MeasureValue = 300_000d, EstimateValue = 600_000d };
        var (session, rt, store, repo, _) = New(costModel: costModel); // default router options (256k window, marginal)
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: seed + propose. Occupancy 20 (<< guard 230,400); R=300000, n=1.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: eNext 600000 >= (R 300000 + C 250000 seed)/1 = 550000 -> economic Transfer (occupancy 20 << guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("reseeded")));      // reseed from transfer
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose
        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(2, rt.OpenSessions);   // original + recycled (economic transfer)
    }

    [Fact]
    public async Task Run_SubThresholdProposals_DoNotAccumulate_IntoTransfer()
    {
        // Sub-window reuse must NOT transfer under the default (250k-seeded) cost estimate: occupancy stays 20
        // (far below the guard), and the marginal rule's amortized average (R + C)/n is dominated by the 250k seed,
        // so the predicted next cost never crosses it. The conservative seed keeps reuse steady until a real
        // transfer measures C.
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H");

        // Round 1: seed + propose (20 tokens).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: another sub-window proposal (occupancy 20). Reuse cost R accrues but stays far below the 250k seed.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D2", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 3: still Continue (the amortized average is dominated by the 250k seed). The extra turns below would
        // satisfy a (wrong) Transfer path, so a regression that transferred is caught by the asserts, not a throw.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>                                     // Continue: proposal | Transfer: delta
            new AgentTurnResult(0, AgentTurnState.Completed, "D3", new AgentTokenUsage(10, 10))));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // Transfer-only: UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("reseeded")));      // Transfer-only: reseed
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D3T")));           // Transfer-only: proposal
        await session.RunAsync(CancellationToken.None);

        // No transfer ever fired: no operational delta, the single warm process reused throughout, round-3 proposal kept.
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal("D3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }
}

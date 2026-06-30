using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class DecisionSessionTests
{
    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con)
        New(int transferThreshold = 200_000)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(transferThreshold));
        return (new DecisionSession(rt, router, art, con, repo), rt, store, repo, con);
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
}

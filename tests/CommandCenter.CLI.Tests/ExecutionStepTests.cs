using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class ExecutionStepTests
{
    private static (ExecutionStep Step, FakeAgentRuntime Rt, MemoryArtifactStore Store, LoopArtifacts Art, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new ExecutionStep(rt, art, con, repo), rt, store, art, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_FirstIteration_UsesStartExecution_WritesHandoff_Verifies()
    {
        var (step, rt, store, _, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("PLAN", prompt);               // StartExecution.Render(plan) includes the plan
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            return Turns.Completed("execution done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Contains("execution done", con.Messages);
    }

    [Fact]
    public async Task Run_WhenAgentDoesNotWriteHandoff_Throws()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Run_WhenTurnNotCompleted_Throws()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
    }
}

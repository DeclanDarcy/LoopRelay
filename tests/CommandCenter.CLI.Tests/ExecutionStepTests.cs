using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
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

    // First iteration: no prior handoff => StartExecution work turn, then a SECOND GenerateHandoff turn
    // (on the same held-open session) writes handoff.md, which is verified afterwards.
    [Fact]
    public async Task Run_FirstIteration_StartExecutionThenGenerateHandoff_WritesHandoff_Verifies()
    {
        var (step, rt, store, _, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("PLAN", prompt);                                   // StartExecution.Render(plan)
            Assert.Contains("start executing the first milestone", prompt);
            return Turns.Completed("work done");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("Write .agents/handoffs/handoff.md", prompt);      // GenerateHandoff.Text
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            return Turns.Completed("handoff done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Contains("work done", con.Messages);
        Assert.Contains("handoff done", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(1, rt.ClosedSessions);
    }

    // A prior live handoff (+ decisions) present => ContinueExecution renders plan + handoff + decisions.
    [Fact]
    public async Task Run_Continuing_UsesContinueExecution_RendersHandoffAndDecisions()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "PRIOR-HANDOFF");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("continue executing the current milestone", prompt);
            Assert.Contains("PRIOR-HANDOFF", prompt);
            Assert.Contains("DECISIONS", prompt);
            return Turns.Completed("continued");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-2").Wait();
            return Turns.Completed("handoff done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(1, rt.ClosedSessions);
    }

    // The handoff turn completed but wrote no handoff.md => the post-turn-2 gate throws, session still closed.
    [Fact]
    public async Task Run_WhenHandoffTurnDoesNotWriteHandoff_Throws_AndClosesSession()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("forgot the handoff")));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }

    // The work turn failed => throw before the handoff turn; session opened and closed.
    [Fact]
    public async Task Run_WhenWorkTurnNotCompleted_Throws_AndClosesSession()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }

    // The handoff turn itself failed => throw; session opened and closed.
    [Fact]
    public async Task Run_WhenHandoffTurnNotCompleted_Throws_AndClosesSession()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }
}

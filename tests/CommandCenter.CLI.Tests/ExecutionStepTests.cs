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

    // With a decisions.md present (the execution agent's system prompt the decision session produced this slice),
    // execution CONTINUES from it: ContinueExecution renders plan + decisions, then a SECOND GenerateHandoff turn
    // (on the same held-open session) writes handoff.md, which is verified afterwards.
    [Fact]
    public async Task Run_ContinueExecutionWithPlanAndDecisions_ThenGenerateHandoff_WritesHandoff_Verifies()
    {
        var (step, rt, store, _, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS-SYS-PROMPT");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("PLAN", prompt);                                   // ContinueExecution.Render(plan, decisions)
            Assert.Contains("DECISIONS-SYS-PROMPT", prompt);
            Assert.Contains("continue executing the current milestone", prompt);
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

    // With NO decisions.md (the first execution of a fresh plan), the work turn uses StartExecution — the plan
    // alone is context enough — then the same held-open session writes handoff.md, verified afterwards.
    [Fact]
    public async Task Run_NoDecisions_StartsFromPlanViaStartExecution_ThenGenerateHandoff()
    {
        var (step, rt, store, _, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN-CONTENT");
        // No decisions.md is written — this is the first execution.

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("PLAN-CONTENT", prompt);                        // StartExecution.Render(plan)
            Assert.Contains("start executing the first milestone", prompt);
            Assert.DoesNotContain("continue executing", prompt);           // not the ContinueExecution prompt
            return Turns.Completed("work done");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("Write .agents/handoffs/handoff.md", prompt);   // GenerateHandoff.Text
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            return Turns.Completed("handoff done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Contains("work done", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(1, rt.ClosedSessions);
    }

    // The execution session must open with codex's full-access sandbox: the persistent app-server turn reaches
    // codex's sandbox via the spec, so the spec the session is opened with is the effective lever. This is scoped
    // to the execution session only — the context-update evolution one-shot stays at its own posture.
    [Fact]
    public async Task Run_OpensExecutionSessionWithDangerFullAccessSandbox()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.True(spec.Sandbox.CanWriteWorkspace);
            return Turns.Completed("work done");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H").Wait();
            return Turns.Completed("handoff done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(1, rt.ClosedSessions);
    }

    // The handoff is consumed by the decision session, NOT rendered into the execution prompt — even when a live
    // handoff exists on disk it must never appear in the ContinueExecution turn ({handoff} was removed from it).
    [Fact]
    public async Task Run_DoesNotRenderHandoffIntoTheExecutionPrompt_EvenWhenOneExists()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "PRIOR-HANDOFF-XYZ");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("continue executing the current milestone", prompt);
            Assert.Contains("DECISIONS", prompt);
            Assert.DoesNotContain("PRIOR-HANDOFF-XYZ", prompt);   // handoff is no longer an execution-prompt input
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
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");
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
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");
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
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "DECISIONS");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }
}

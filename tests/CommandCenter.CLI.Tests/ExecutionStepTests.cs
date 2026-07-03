using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class ExecutionStepTests
{
    private static (ExecutionStep Step, FakeAgentRuntime Rt, MemoryArtifactStore Store, LoopArtifacts Art, Repository Repo, RecordingLoopConsole Con) New(
        FakeProcessRunner? git = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        // Default git: the work turn produced a real change, so turn 2 is the ordinary GenerateHandoff —
        // the shape every pre-existing test narrates. No-changes tests pass their own runner.
        git ??= StatusRunner(" M src/Foo.cs");
        var detector = new WorkingTreeChangeDetector(git, repo);
        var milestones = new MilestoneGate(store, repo);
        return (new ExecutionStep(rt, art, con, repo, detector, milestones), rt, store, art, repo, con);
    }

    /// <summary>Scripts a runner whose `git status` always returns the given porcelain; everything else succeeds.</summary>
    private static FakeProcessRunner StatusRunner(string porcelain) => new()
    {
        Handler = (_, args) => args[0] == "status"
            ? FakeProcessRunner.Ok(porcelain)
            : FakeProcessRunner.Ok()
    };

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

    // ----- Turn-2 prompt selection (change detection between the work turn and the handoff turn) -----

    /// <summary>Enqueues a turn-2 handler that captures its prompt and writes the handoff file.</summary>
    private static Func<string?> CaptureHandoffTurn(FakeAgentRuntime rt, Repository repo)
    {
        string? captured = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            captured = prompt;
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H").Wait();
            return Turns.Completed("handoff done");
        }));
        return () => captured;
    }

    // Real (non-.agents) changed paths after the work turn => the ordinary GenerateHandoff, verbatim.
    [Fact]
    public async Task Run_WithRealChanges_Turn2IsGenerateHandoffText_WithItsPhase()
    {
        var (step, rt, store, _, repo, con) = New();   // default git reports " M src/Foo.cs"
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        Func<string?> turn2 = CaptureHandoffTurn(rt, repo);

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(GenerateHandoff.Text, turn2());
        Assert.Contains(("phase", "Execution: GenerateHandoff"), con.Events);
        Assert.DoesNotContain(("phase", "Execution: GenerateNoChangesHandoff"), con.Events);
    }

    // No real changes (a lone .agents gitlink is NOT progress — CommitGate's exact rule) => turn 2 is
    // GenerateNoChangesHandoff carrying the still-unticked milestone items, in order, joined with \n.
    [Fact]
    public async Task Run_NoRealChanges_Turn2IsGenerateNoChangesHandoff_WithUntickedItems()
    {
        var (step, rt, store, _, repo, con) = New(StatusRunner(" M .agents"));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [ ] first\n- [x] done");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"), "- [ ] second");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        Func<string?> turn2 = CaptureHandoffTurn(rt, repo);

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(GenerateNoChangesHandoff.Render("- [ ] first\n- [ ] second"), turn2());
        Assert.Contains(("phase", "Execution: GenerateNoChangesHandoff"), con.Events);
        Assert.DoesNotContain(("phase", "Execution: GenerateHandoff"), con.Events);
    }

    // No changes AND nothing unticked: the path is chosen purely by change detection, so the no-changes
    // prompt still goes out — with an empty item list.
    [Fact]
    public async Task Run_NoRealChanges_EmptyUntickedList_StillTakesNoChangesPath()
    {
        var (step, rt, store, _, repo, con) = New(StatusRunner(string.Empty));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] all done");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("work done")));
        Func<string?> turn2 = CaptureHandoffTurn(rt, repo);

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(GenerateNoChangesHandoff.Render(string.Empty), turn2());
        Assert.Contains(("phase", "Execution: GenerateNoChangesHandoff"), con.Events);
    }

    // Detection must run AFTER the work turn: the tree is clean until the work turn dirties it, and the
    // handoff still comes out as the ordinary GenerateHandoff. Probing before turn 1 would see the clean
    // tree and mis-route to the no-changes prompt.
    [Fact]
    public async Task Run_ChangesMadeByTheWorkTurnItself_CountAsChanges()
    {
        bool workRan = false;
        var git = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] == "status"
                ? FakeProcessRunner.Ok(workRan ? " M src/New.cs" : string.Empty)
                : FakeProcessRunner.Ok()
        };
        var (step, rt, store, _, repo, _) = New(git);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            workRan = true;
            return Turns.Completed("work done");
        }));
        Func<string?> turn2 = CaptureHandoffTurn(rt, repo);

        await step.RunAsync(CancellationToken.None);

        Assert.Equal(GenerateHandoff.Text, turn2());
    }
}

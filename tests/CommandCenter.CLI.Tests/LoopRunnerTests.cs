using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class LoopRunnerTests
{
    private sealed record Harness(
        LoopRunner Runner, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con,
        FakeProcessRunner Git);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        var gate = new MilestoneGate(store, repo);
        // These tests exercise loop orchestration only; the Codex usage gate is unit-tested separately
        // (UsageGateTests) and its per-turn placement in GatedAgentRuntimeTests, so the loop runs ungated.
        var exec = new ExecutionStep(rt, art, con, repo);
        var dec = new DecisionSession(rt, router, art, con, repo);
        // By default `git status` reports an EMPTY working tree, so the submodule publisher is a no-op and
        // the gate skips commit/push — the existing single-iteration tests reach their asserted outcome
        // before it could ever trip.
        var git = new FakeProcessRunner { Handler = (_, _) => FakeProcessRunner.Ok() };
        // CommitGate IGNORES `.agents`; the submodule is committed+pushed only by the publisher (pre-codex).
        var submodulePublisher = new AgentsSubmodulePublisher(git, repo, con);
        var commitGate = new CommitGate(git, repo, con);
        return new Harness(
            new LoopRunner(gate, art, exec, dec, submodulePublisher, commitGate, con), rt, store, repo, con, git);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    private static List<string> Phases(RecordingLoopConsole con) =>
        con.Events.Where(e => e.Kind == "phase").Select(e => e.Text).ToList();

    [Fact]
    public async Task Run_WhenEpicAlreadyComplete_ReturnsEpicCompletedImmediately()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] done");

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Empty(h.Rt.OneShotCalls);   // no codex run at all
    }

    [Fact]
    public async Task Run_FirstIteration_DecisionThenExecution_ThenCompletes()
    {
        var h = New();
        // milestone incomplete at first, becomes complete after the execution agent "checks the box".
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] task");

        // Decision-first: the decision session seeds then proposes decisions.md (the first execution agent's
        // system prompt, since no handoff exists yet). THEN the execution session runs two turns off the same
        // SessionTurns queue: turn 1 does the work (checks the milestone box, so the epic completes next
        // LoopStart), turn 2 writes handoff.md.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] task").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            return Turns.Completed("handoff");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal("DECISIONS-1", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        // After one iteration: live handoff present (written by execution, rotated next loop only).
        Assert.True(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));

        // The decision phase ran BEFORE the execution phase (the sequencing was swapped to decision-first).
        List<string> phases = Phases(h.Con);
        int decisionIndex = phases.FindIndex(p => p.StartsWith("Decision"));
        int executionIndex = phases.FindIndex(p => p.StartsWith("Execution"));
        Assert.True(decisionIndex >= 0 && executionIndex >= 0, "both phases must run");
        Assert.True(decisionIndex < executionIndex, "decision must precede execution");
    }

    [Fact]
    public async Task Run_ResumeWithExistingHandoff_DecisionConsumesItThenExecutionRuns()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-RESUME");

        // Decision seeds + proposes over the resumed handoff, THEN execution runs and completes the milestone.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("H-RESUME", prompt);   // the resumed handoff folds into the next system prompt
            return Turns.Completed("DEC-RESUME");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-NEXT").Wait();
            return Turns.Completed("handoff");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal("DEC-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        // The consumed handoff was rotated to handoff.0001.md before execution wrote the next live handoff.
        Assert.Equal("H-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Run_WhenDecisionStepFails_ReturnsFailed()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // Decision runs first: seed completes, but the proposal turn fails -> DecisionSession throws -> Failed.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Run_WhenExecutionStepFails_ReturnsFailed()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // Decision succeeds (seed + propose), then the execution work turn fails -> ExecutionStep throws -> Failed.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Run_WhenOnlyBookkeepingChangesRepeat_ReturnsStalled()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        // The epic never completes: the milestone box stays unchecked for the whole run.
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");

        // The parent working tree reports ONLY the `.agents` submodule gitlink every iteration (the
        // decisions/handoff churn now lives inside the submodule); the submodule reports its own dirty
        // content and a tracking branch so the publisher can commit+push it.
        h.Git.Handler = (_, args) => args[0] switch
        {
            "status" => FakeProcessRunner.Ok(" M .agents"),
            "branch" => FakeProcessRunner.Ok("main"),
            _ => FakeProcessRunner.Ok()
        };

        // Each iteration runs decision-first: the decision seed (first iteration only), the decision proposal
        // (persists decisions.md), then execution's work turn and handoff turn (writes a fresh handoff). Script
        // generously to cover >3 iterations; the stall gate trips first.
        for (int i = 0; i < 6; i++)
        {
            if (i == 0)
            {
                h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
            }
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed($"DECISIONS-{i}")));
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed($"executed-{i}")));
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
            {
                s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), $"HANDOFF-{i}").Wait();
                return Turns.Completed($"handoff-{i}");
            }));
        }

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Stalled, outcome);
    }

    [Fact]
    public async Task Run_WhenCancelled_ReturnsCancelled()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        using var cts = new CancellationTokenSource();
        // The decision seed turn (first SessionTurns entry, since decision now runs first) cancels mid-flight.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(cts.Token);

        Assert.Equal(LoopOutcome.Cancelled, outcome);
    }

    [Fact]
    public async Task Run_CommitsAndPushesAgentsSubmodule_BeforeInvokingCodex()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");

        // A shared timeline records the FIRST submodule push (the pre-codex publish) and the moment codex's
        // execution work turn runs, so we can assert the submodule was persisted before codex was invoked.
        var timeline = new List<string>();
        h.Git.Handler = (wd, args) =>
        {
            bool submodule = wd.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);
            if (args[0] == "status")
            {
                return FakeProcessRunner.Ok(submodule ? " M decisions/decisions.md" : " M .agents");
            }

            if (args[0] == "branch")
            {
                return FakeProcessRunner.Ok("main");
            }

            if (submodule && args[0] == "push")
            {
                timeline.Add("submodule-push");
            }

            return FakeProcessRunner.Ok();
        };

        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DEC")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            timeline.Add("codex-exec");   // the execution work turn = "invoking codex"
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H").Wait();
            return Turns.Completed("handoff");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        int firstSubmodulePush = timeline.IndexOf("submodule-push");
        int codexExec = timeline.IndexOf("codex-exec");
        int lastSubmodulePush = timeline.LastIndexOf("submodule-push");
        Assert.True(firstSubmodulePush >= 0, "the .agents submodule must be committed+pushed");
        Assert.True(codexExec >= 0, "codex must run");
        Assert.True(firstSubmodulePush < codexExec, "the .agents submodule must be pushed BEFORE codex runs");
        // ...and codex's own writes (milestone box-check, handoff) are published AFTER the codex turn, so the
        // completing iteration's state reaches the submodule remote.
        Assert.True(lastSubmodulePush > codexExec, "the .agents submodule must also be pushed AFTER codex runs");
    }

    // The parent repo's `.agents` gitlink is committed (staged as `.agents`, message = GitlinkPointerMessage)
    // BEFORE codex's execution turn, so the tree codex opens on is clean rather than showing a dirty submodule
    // pointer left by the pre-codex submodule publish.
    [Fact]
    public async Task Run_RecordsParentAgentsGitlink_BeforeInvokingCodex()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");

        // Submodule reads dirty (so its pointer moves); the parent then reads its `.agents` gitlink modified.
        var timeline = new List<string>();
        h.Git.Handler = (wd, args) =>
        {
            bool submodule = wd.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);
            if (args[0] == "status")
            {
                return FakeProcessRunner.Ok(submodule ? " M decisions/decisions.md" : " M .agents");
            }

            if (args[0] == "branch")
            {
                return FakeProcessRunner.Ok("main");
            }

            // The parent-repo commit carrying the gitlink message marks when the pointer was versioned.
            if (!submodule && args[0] == "commit" && args.Count >= 3 &&
                args[2] == AgentsSubmodulePublisher.GitlinkPointerMessage)
            {
                timeline.Add("parent-gitlink-commit");
            }

            return FakeProcessRunner.Ok();
        };

        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DEC")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            timeline.Add("codex-exec");   // the execution work turn = "invoking codex"
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H").Wait();
            return Turns.Completed("handoff");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        int parentGitlinkCommit = timeline.IndexOf("parent-gitlink-commit");
        int codexExec = timeline.IndexOf("codex-exec");
        Assert.True(parentGitlinkCommit >= 0, "the parent .agents gitlink must be committed");
        Assert.True(codexExec >= 0, "codex must run");
        Assert.True(parentGitlinkCommit < codexExec, "the parent gitlink must be committed BEFORE codex runs");
    }

    // Scripts git so the `.agents` submodule always reads dirty (on branch main) and the parent reads clean,
    // so any publish — including the on-exit salvage — actually commits.
    private static void DirtySubmoduleCleanParent(Harness h) =>
        h.Git.Handler = (wd, args) =>
        {
            bool submodule = wd.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);
            return args[0] switch
            {
                "status" => FakeProcessRunner.Ok(submodule ? " M decisions/decisions.md" : string.Empty),
                "branch" => FakeProcessRunner.Ok("main"),
                _ => FakeProcessRunner.Ok()
            };
        };

    private static bool IsPartialExitCommit((string FileName, IReadOnlyList<string> Args, string WorkingDirectory) call) =>
        call.Args.Count >= 3 && call.Args[0] == "commit" &&
        call.Args[2] == AgentsSubmodulePublisher.PartialExitMessage;

    [Fact]
    public async Task Run_WhenExecutionFails_SalvagesPartialAgentsStateOnExit()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        DirtySubmoduleCleanParent(h);

        // Decision succeeds (seed + propose); the execution work turn fails -> ExecutionStep throws -> Failed.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
        Assert.Contains(h.Git.Calls, IsPartialExitCommit);
    }

    [Fact]
    public async Task Run_WhenCancelled_SalvagesPartialAgentsStateOnExit()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        DirtySubmoduleCleanParent(h);

        using var cts = new CancellationTokenSource();
        // The decision seed turn cancels mid-flight (before any pre-codex publish runs).
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(cts.Token);

        Assert.Equal(LoopOutcome.Cancelled, outcome);
        // The salvage runs under CancellationToken.None, so it still commits despite the cancelled run.
        Assert.Contains(h.Git.Calls, IsPartialExitCommit);
    }

    [Fact]
    public async Task Run_WhenSalvagePushFails_StaysFailed_BestEffort()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");

        // Submodule dirty on a branch, but every push fails. The decision proposal turn fails first (-> Failed);
        // the on-exit salvage then also fails to push and MUST be swallowed rather than masking the outcome.
        h.Git.Handler = (wd, args) =>
        {
            bool submodule = wd.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);
            return args[0] switch
            {
                "status" => FakeProcessRunner.Ok(submodule ? " M decisions/decisions.md" : string.Empty),
                "branch" => FakeProcessRunner.Ok("main"),
                "push" => FakeProcessRunner.Fail("push rejected"),
                _ => FakeProcessRunner.Ok()
            };
        };

        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
    }
}

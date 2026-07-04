using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class LoopRunnerTests
{
    private sealed record Harness(
        LoopRunner Runner, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con,
        FakeProcessRunner Git, FakeDecisionSessionResumeStore Resume);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        var gate = new MilestoneGate(store, repo);
        // By default `git status` reports an EMPTY working tree, so the submodule publisher is a no-op, the
        // gate skips commit/push — the existing single-iteration tests reach their asserted outcome before it
        // could ever trip — and every execution slice takes the no-changes handoff (these tests only assert
        // the "Execution" phase prefix and the handoff file, both identical across the two handoff prompts).
        var git = new FakeProcessRunner { Handler = (_, _) => FakeProcessRunner.Ok() };
        var detector = new WorkingTreeChangeDetector(git, repo);
        // These tests exercise loop orchestration only; usage-limit detection is unit-tested separately
        // (UsageLimitDetectorTests) and its per-turn retry seam in GatedAgentRuntimeTests, so the loop
        // runs unwatched.
        var exec = new ExecutionStep(rt, art, con, repo, detector, gate);
        var dec = new DecisionSession(rt, router, art, con, repo);
        // CommitGate IGNORES `.agents`; the submodule is committed+pushed only by the publisher (pre-codex).
        var submodulePublisher = new AgentsSubmodulePublisher(git, repo, con);
        var commitGate = new CommitGate(detector, git, repo, con);
        var resume = new FakeDecisionSessionResumeStore();
        return new Harness(
            new LoopRunner(gate, art, exec, dec, submodulePublisher, commitGate, resume, con),
            rt, store, repo, con, git, resume);
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
    public async Task Run_FirstIteration_NoHandoff_ExecutionFirstFromPlan_NoDecision_ThenCompletes()
    {
        var h = New();
        // milestone incomplete at first, becomes complete after the execution agent "checks the box".
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] task");

        // First pass, no handoff yet: execution runs FIRST, straight from the plan via StartExecution — NO decision
        // session. The execution session runs two turns off the SessionTurns queue: turn 1 does the work (checks the
        // milestone box, so the epic completes next LoopStart), turn 2 writes handoff.md. No decisions.md is produced.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Contains("PLAN", prompt);                                   // StartExecution.Render(plan)
            Assert.Contains("start executing the first milestone", prompt);
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
        // No decision ran, so no decisions.md was produced (neither live nor a numbered snapshot).
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        // After one iteration: live handoff present (written by execution).
        Assert.True(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));

        // Execution ran with NO preceding decision phase.
        List<string> phases = Phases(h.Con);
        Assert.DoesNotContain(phases, p => p.StartsWith("Decision"));
        Assert.Contains(phases, p => p.StartsWith("Execution"));
    }

    [Fact]
    public async Task Run_ResumeWithExistingHandoff_DecisionConsumesItThenExecutionRuns()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-RESUME");

        // Decision proposes over the resumed handoff (context + handoff folded into one fresh-process turn), THEN
        // execution runs and completes the milestone.
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
        // Decision proposal persisted then retired after execution consumed it — assert on the numbered snapshot.
        Assert.Equal("DEC-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        // The consumed handoff was rotated to handoff.0001.md before execution wrote the next live handoff.
        Assert.Equal("H-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Run_WhenUnrotatedDecisionsExist_SkipsDecisionSession_AndExecutesDirectly()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // A prior slice already produced decisions.md but never consumed+retired it (unrotated/pending).
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions), "PENDING-DECISIONS");

        // Only the execution session's two turns are scripted — NO decision turns. The work turn
        // runs against the pending decisions.md and completes the milestone so the loop stops after one slice.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Contains("PENDING-DECISIONS", prompt);   // execution consumes the pending decisions.md directly
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
        // The decision session never ran (no "Decision" phase) — the loop skipped straight to execution.
        Assert.DoesNotContain(Phases(h.Con), p => p.StartsWith("Decision"));
        Assert.Contains(Phases(h.Con), p => p.StartsWith("Execution"));
        // The pending decisions.md was retired once execution consumed it.
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task Run_AfterExecutionConsumesDecisions_RetiresLiveDecisions()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // A handoff exists, so this slice is decision-first (a handoff is what the decision session folds in).
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-0");

        // No pending decisions.md, so the decision session runs (one proposal turn) and persists decisions.md; the
        // execution slice then consumes it and the loop retires the live file. Epic completes after one slice.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H").Wait();
            return Turns.Completed("handoff");
        }));

        await h.Runner.RunAsync(CancellationToken.None);

        // Live decisions.md retired (consumed); the numbered snapshot from persist is the retained history.
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("DECISIONS-1", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    // Core resume invariant: the retire sits AFTER execution, so an execution-turn failure (which throws) never
    // reaches it — the pending decisions.md MUST survive so a restart takes the skip path and re-executes rather
    // than paying for a fresh decision. A refactor that retired decisions before/around execution would silently
    // defeat the whole optimization; this pins it.
    [Fact]
    public async Task Run_WhenExecutionFails_LeavesDecisionsPendingForRestart()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // A handoff exists, so this slice is decision-first — the decision produces the decisions.md whose survival
        // across an execution failure this test pins.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-0");

        // Decision succeeds (one proposal turn -> persists decisions.md); the execution work turn then fails.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
        // Retire (after execution) never ran because execution threw — decisions.md stays pending for the restart-skip.
        Assert.True(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
    }

    // Two producing slices run a FRESH decision each: slice 1 persists+retires decisions.md, and because retire
    // dropped the live file, slice 2 does NOT take the skip path — it runs its own decision and persists
    // decisions.0002.md. Guards against a broken retire that would strand a live decisions.md and make every later
    // slice wrongly skip the decision session.
    [Fact]
    public async Task Run_TwoProducingSlices_RunFreshDecisionEach_RetiringBetween()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // A handoff exists from the start, so BOTH slices are decision-first (a fresh decision each), which is what
        // this test pins — without it, slice 1 (no handoff) would be execution-first and run no decision.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-0");

        // Slice 1: propose (DECISIONS-1) on the fresh process, execution work (does NOT complete the milestone) + handoff.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("exec-1")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-1").Wait();
            return Turns.Completed("handoff-1");
        }));
        // Slice 2: propose again (already seeded), execution work COMPLETES the milestone + handoff.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-2")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("exec-2");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-2").Wait();
            return Turns.Completed("handoff-2");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        // Both slices ran their OWN decision (skip never fired): two numbered snapshots, two decision runs.
        Assert.Equal("DECISIONS-1", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("DECISIONS-2", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalDecision(2))));
        // Count the routing header (one per decision RUN) — each run also emits sub-phases such as
        // "Decision: Propose", so a bare "Decision" prefix would over-count.
        Assert.Equal(2, Phases(h.Con).Count(p => p.StartsWith("Decision (route=")));
        // Final decisions.md retired after the last slice's execution.
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
    }

    // On the skip path a lingering live handoff (left by a crash before its rotation) is NOT archived by the loop:
    // the skip bypasses RotateLiveHandoffAsync and execution's turn-2 write overwrites it. Pins the documented
    // skip-of-rotation behaviour so an accidental UNCONDITIONAL rotation (which would spuriously archive it) is caught.
    [Fact]
    public async Task Run_SkipPath_DoesNotRotateALingeringLiveHandoff()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions), "PENDING-DECISIONS");
        // A live handoff lingers alongside the pending decisions (the mid-slice crash state that triggers a skip).
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-LINGER");

        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-NEW").Wait();
            return Turns.Completed("handoff");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        // The skip path never ran the decision session, so it never rotated the lingering handoff to numbered history...
        Assert.DoesNotContain(Phases(h.Con), p => p.StartsWith("Decision"));
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
        // ...execution simply overwrote the live handoff with its own.
        Assert.Equal("H-NEW", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));
    }

    [Fact]
    public async Task Run_WhenDecisionStepFails_ReturnsFailed()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // A handoff exists, so this slice is decision-first; the decision's proposal turn fails ->
        // DecisionSession throws -> Failed.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-0");
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
        // First pass (no handoff): execution runs first from the plan; its work turn fails -> ExecutionStep throws -> Failed.
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
        // A handoff exists from the start so EVERY iteration is decision-first (uniform decision+work+handoff),
        // which the loop scripting below assumes.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-init");

        // The parent working tree reports ONLY the `.agents` submodule gitlink every iteration (the
        // decisions/handoff churn now lives inside the submodule); the submodule reports its own dirty
        // content and a tracking branch so the publisher can commit+push it.
        h.Git.Handler = (_, args) => args[0] switch
        {
            "status" => FakeProcessRunner.Ok(" M .agents"),
            "branch" => FakeProcessRunner.Ok("main"),
            _ => FakeProcessRunner.Ok()
        };

        // Each iteration runs decision-first: the decision proposal (persists decisions.md), then execution's
        // work turn and handoff turn (writes a fresh handoff). Script generously to cover >3 iterations; the
        // stall gate trips first.
        for (int i = 0; i < 6; i++)
        {
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

    // A reduction in unchecked milestone items IS substantive progress: an iteration that only ticks
    // milestone boxes (which live inside the ignored `.agents/`) must reset the stall counter exactly like
    // a real code change would, so a box-ticking-only epic runs to completion instead of stalling at 3.
    [Fact]
    public async Task Run_WhenOnlyMilestoneBoxesAreTickedEachIteration_DoesNotStall_CompletesEpic()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        // FOUR unchecked boxes and one ticked per iteration: without milestone-progress credit the stall
        // gate would trip on the third no-change iteration, before the epic could ever complete.
        string[] items = ["a", "b", "c", "d"];
        await h.Store.WriteAsync(
            Resolve(h.Repo, ".agents/milestones/m1.md"), string.Join("\n", items.Select(it => $"- [ ] {it}")));
        // A handoff exists from the start so EVERY iteration is decision-first (uniform decision+work+handoff).
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-init");

        // The parent working tree reports ONLY the `.agents` submodule gitlink every iteration — no real
        // repository change ever appears; milestone box-ticks are the only progress being made.
        h.Git.Handler = (_, args) => args[0] switch
        {
            "status" => FakeProcessRunner.Ok(" M .agents"),
            "branch" => FakeProcessRunner.Ok("main"),
            _ => FakeProcessRunner.Ok()
        };

        // Each iteration's work turn ticks exactly one more box; after the fourth the epic completes.
        for (int i = 0; i < items.Length; i++)
        {
            int ticked = i + 1;
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed($"DECISIONS-{ticked}")));
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
            {
                string content = string.Join(
                    "\n", items.Select((it, j) => j < ticked ? $"- [x] {it}" : $"- [ ] {it}"));
                s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), content).Wait();
                return Turns.Completed($"executed-{ticked}");
            }));
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
            {
                s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), $"H-{ticked}").Wait();
                return Turns.Completed($"handoff-{ticked}");
            }));
        }

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
    }

    [Fact]
    public async Task Run_WhenCancelled_ReturnsCancelled()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        using var cts = new CancellationTokenSource();
        // First pass (no handoff) is execution-first, so the first execution turn cancels mid-flight.
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

        // First pass (no handoff): execution runs first; its work turn fails -> ExecutionStep throws -> Failed.
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
        // First pass (no handoff) is execution-first, so the first execution turn cancels mid-flight.
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

        // Submodule dirty on a branch, but every push fails. A handoff exists so this slice is decision-first; the
        // decision proposal turn fails first (-> Failed) BEFORE any pre-codex publish, so the on-exit salvage is the
        // only push — and it too fails and MUST be swallowed rather than masking the outcome.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-0");
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

        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Run_WhenEpicComplete_ClearsThePersistedDecisionSessionState()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] done");
        h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal(1, h.Resume.ClearCalls);   // idempotent: re-runs against a completed epic re-delete a no-op
        Assert.Null(h.Resume.State);
    }
}

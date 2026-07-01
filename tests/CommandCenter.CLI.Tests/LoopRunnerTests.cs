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
        FakeProcessRunner Git, FakeCodexUsageProbe Usage, FakeUsageDelay Delay);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        var gate = new MilestoneGate(store, repo);
        var exec = new ExecutionStep(rt, art, con, repo);
        var dec = new DecisionSession(rt, router, art, con, repo);
        // By default `git status` reports an EMPTY working tree, so the gate skips commit/push and the
        // existing single-iteration tests reach their asserted outcome before it could ever trip.
        var git = new FakeProcessRunner { Handler = _ => FakeProcessRunner.Ok() };
        var commitGate = new CommitGate(git, repo, con);
        // Usage gate: full capacity by default so it never delays and the existing loop tests are unaffected.
        var usage = new FakeCodexUsageProbe { Default = new CodexUsageStatus(100, TimeSpan.Zero, 100, TimeSpan.Zero) };
        var delay = new FakeUsageDelay();
        var usageGate = new UsageGate(usage, delay, con);
        return new Harness(
            new LoopRunner(usageGate, gate, art, exec, dec, commitGate, con), rt, store, repo, con, git, usage, delay);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_WhenEpicAlreadyComplete_DoesNotWaitForUsageEvenIfExhausted()
    {
        var h = New();
        // A finished epic must return immediately — it needs no Codex work, so it must NOT block on quota.
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] done");
        h.Usage.Default = new CodexUsageStatus(0, TimeSpan.FromMinutes(30), 0, TimeSpan.FromDays(5));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Empty(h.Delay.Delays);
    }

    [Fact]
    public async Task Run_WhenWorkRemainsAndUsageExhausted_WaitsOnceBeforeRunningCodex()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] task");
        h.Usage.Default = new CodexUsageStatus(0, TimeSpan.FromMinutes(30), 50, TimeSpan.FromHours(1));

        // Branch A: execution work turn checks the box (epic completes next iteration), handoff turn, then
        // the decision seed + proposal.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] task").Wait();
            return Turns.Completed("executed");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF").Wait();
            return Turns.Completed("handoff");
        }));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS")));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        // Waited exactly once — on the work iteration, before codex ran; the completing iteration returns
        // via the epic-complete check without reaching the usage gate.
        Assert.Equal(new[] { TimeSpan.FromMinutes(30) }, h.Delay.Delays);
    }

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
    public async Task Run_FirstIterationBranchA_RunsExecutionThenDecision_ThenCompletes()
    {
        var h = New();
        // milestone incomplete at first, becomes complete after the execution agent "checks the box".
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] task");

        // Execution is now a held-open session of TWO turns (the same SessionTurns queue the decision
        // session draws from, consumed first): turn 1 does the work (checks the milestone box, so the epic
        // completes next LoopStart), turn 2 writes handoff.md.
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
        // Decision session: seed then propose.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal("DECISIONS-1", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        // After one Branch-A iteration: live handoff present (written by execution, rotated next loop only).
        Assert.True(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));
    }

    [Fact]
    public async Task Run_BranchB_DecisionsAbsentHandoffPresent_RunsDecisionOnly()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-RESUME");

        // Decision-only: seed + propose. After it, mark milestone done so the loop stops.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("DEC-B");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Empty(h.Rt.OneShotCalls);   // Branch B never runs execution
        // handoff rotated away (archived + live deleted) in Branch B.
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal("H-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Run_WhenStepFails_ReturnsFailed()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        // The execution work turn fails, so ExecutionStep throws a LoopStepException -> the loop returns Failed.
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

        // git status always reports ONLY bookkeeping paths (the every-iteration decisions+handoff churn).
        h.Git.Handler = args => args[0] == "status"
            ? FakeProcessRunner.Ok(" M .agents/decisions/decisions.md\n M .agents/handoffs/handoff.md")
            : FakeProcessRunner.Ok();

        // Each iteration runs Branch A. Execution and the decision session now share the SessionTurns queue,
        // consumed in order per iteration: execution work turn, execution handoff turn (writes a fresh
        // handoff), the decision seed (first iteration only), then the decision proposal (persists
        // decisions.md). Script generously to cover >3 iterations; the stall gate trips first.
        for (int i = 0; i < 6; i++)
        {
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed($"executed-{i}")));
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
            {
                s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), $"HANDOFF-{i}").Wait();
                return Turns.Completed($"handoff-{i}");
            }));
            if (i == 0)
            {
                h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
            }
            h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed($"DECISIONS-{i}")));
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
        // The execution work turn (first SessionTurns entry) cancels mid-flight.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(cts.Token);

        Assert.Equal(LoopOutcome.Cancelled, outcome);
    }
}

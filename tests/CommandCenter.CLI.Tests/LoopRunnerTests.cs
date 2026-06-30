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
        var exec = new ExecutionStep(rt, art, con, repo);
        var dec = new DecisionSession(rt, router, art, con, repo);
        // By default `git status` reports an EMPTY working tree, so the gate skips commit/push and the
        // existing single-iteration tests reach their asserted outcome before it could ever trip.
        var git = new FakeProcessRunner { Handler = _ => FakeProcessRunner.Ok() };
        var commitGate = new CommitGate(git, repo, con);
        return new Harness(new LoopRunner(gate, art, exec, dec, commitGate, con), rt, store, repo, con, git);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

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

        // Execution one-shot: writes handoff.md AND checks the milestone box (epic completes next LoopStart).
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] task").Wait();
            return Turns.Completed("executed");
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
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("no handoff written")));

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

        // Each iteration runs Branch A: one execution one-shot (writes a fresh handoff) + one decision
        // proposal (persists decisions.md). Script generously to cover >3 iterations; the loop stalls first.
        for (int i = 0; i < 6; i++)
        {
            h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
            {
                s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), $"HANDOFF-{i}").Wait();
                return Turns.Completed($"executed-{i}");
            }));
        }
        // Decision session: one seed (first iteration only), then one proposal per iteration.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        for (int i = 0; i < 6; i++)
        {
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
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(cts.Token);

        Assert.Equal(LoopOutcome.Cancelled, outcome);
    }
}

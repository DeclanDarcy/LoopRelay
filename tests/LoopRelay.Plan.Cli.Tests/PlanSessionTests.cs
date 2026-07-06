using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class PlanSessionTests
{
    private static (Cli.PlanSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new Cli.PlanArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new Cli.PlanSession(rt, artifacts, con, repo), rt, store, repo, con);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Fact]
    public async Task HappyPath_WritePlanThenRevise_RunOnTheSameSession_AndCloseFiresOnce()
    {
        var (session, rt, store, repo, _) = New();

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(WritePlan.Text, prompt);
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN V1").Wait();
            return Turns.Completed("wrote plan");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(RevisePlan.Render("FEEDBACK"), prompt);
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN V2").Wait();
            return Turns.Completed("revised plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);
        await session.ReviseAsync("FEEDBACK", CancellationToken.None);

        // Both turns ran on the one session opened by WritePlanAsync — ReviseAsync never reopens.
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(0, rt.ClosedSessions);

        await session.CloseAsync();

        Assert.Equal(1, rt.ClosedSessions);
        Assert.Equal("PLAN V2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task WritePlanAsync_OpensDangerFullAccessSessionAtRepoRootWithXhighEffort()
    {
        var (session, rt, _, repo, _) = New();

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, _, s) =>
        {
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.True(spec.Sandbox.CanWriteWorkspace);
            Assert.True(spec.Sandbox.CanAccessNetwork);
            Assert.False(spec.Sandbox.RequiresApproval);
            Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal(repo.Path, spec.WorkingDirectory);
            Assert.Equal(SessionRole.Planning, spec.Role);
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);
    }

    [Fact]
    public async Task WritePlanAsync_EchoesOutputWhenNothingStreamed()
    {
        var (session, rt, _, repo, con) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan output");
        }));

        await session.WritePlanAsync(CancellationToken.None);

        Assert.Contains("wrote plan output", con.Messages);
    }

    [Fact]
    public async Task WritePlanAsync_TurnFailed_ThrowsWithDiagnosticsTail_AndClosesSession()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "write-plan stderr tail")));

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => session.WritePlanAsync(CancellationToken.None));

        Assert.Contains("write-plan stderr tail", ex.Message);
        Assert.Contains("Agent stderr (tail):", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task WritePlanAsync_TurnFailedWithoutDiagnostics_ThrowsWithoutDiagnosticsSuffix()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom")));

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => session.WritePlanAsync(CancellationToken.None));

        Assert.DoesNotContain("Agent stderr (tail):", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task WritePlanAsync_CompletedButNoPlanWritten_Throws_AndClosesSession()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => session.WritePlanAsync(CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task WritePlanAsync_CompletedButPlanIsWhitespaceOnly_Throws_AndClosesSession()
    {
        var (session, rt, _, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "   \n  ").Wait();
            return Turns.Completed("wrote nothing meaningful");
        }));

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => session.WritePlanAsync(CancellationToken.None));

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task ReviseAsync_TurnFailed_ThrowsWithDiagnosticsTail_AndClosesSession()
    {
        var (session, rt, _, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "revise stderr tail")));

        await session.WritePlanAsync(CancellationToken.None);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => session.ReviseAsync("FEEDBACK", CancellationToken.None));

        Assert.Contains("revise stderr tail", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task ReviseAsync_CompletedButLeavesPlanMissing_Throws_AndClosesSession()
    {
        var (session, rt, store, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("deleted the plan"))
        );

        await session.WritePlanAsync(CancellationToken.None);
        // Simulate the revise turn deleting the plan file outright.
        await store.DeleteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan));

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task ReviseAsync_TurnEmptiesPlan_Throws_AndClosesSession()
    {
        var (session, rt, _, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), string.Empty).Wait();
            return Turns.Completed("truncated the plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task ReviseAsync_CalledWithoutPriorWritePlanAsync_Throws()
    {
        var (session, rt, _, _, _) = New();

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

        Assert.Equal(0, rt.OpenSessions);
        Assert.Equal(0, rt.ClosedSessions);
    }

    [Fact]
    public async Task CloseAsync_CalledTwice_ClosesUnderlyingSessionOnlyOnce()
    {
        var (session, rt, _, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);
        await session.CloseAsync();
        await session.CloseAsync();

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task DisposeAsync_BehavesLikeCloseAsync_AndIsAlsoIdempotent()
    {
        var (session, rt, _, repo, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN").Wait();
            return Turns.Completed("wrote plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);
        await session.DisposeAsync();
        await session.CloseAsync();

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task WritePlanAsync_Failure_AlreadyClosesSession_SoALaterExplicitCloseIsANoOp()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => session.WritePlanAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);

        await session.CloseAsync();
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task CloseAsync_BeforeAnySessionWasOpened_IsANoOp()
    {
        var (session, rt, _, _, _) = New();

        await session.CloseAsync();

        Assert.Equal(0, rt.OpenSessions);
        Assert.Equal(0, rt.ClosedSessions);
    }
}

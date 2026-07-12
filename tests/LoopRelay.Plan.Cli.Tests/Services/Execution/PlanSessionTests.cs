using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Execution;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using LoopRelay.Plan.Cli.Tests.Models;
using LoopRelay.Plan.Cli.Tests.Services.Agents;
using LoopRelay.Plan.Cli.Tests.Services.Support;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class PlanSessionTests
{
    private static (PlanSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new PlanSession(rt, artifacts, con, repo), rt, store, repo, con);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Fact]
    public void Plan_templates_do_not_embed_resolved_prompt_policy_profiles()
    {
        Assert.DoesNotContain("## Implementation-First Prompt Policy", WritePlan.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository growth is implementation-first", WritePlan.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Resolved Prompt Policy", WritePlan.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Implementation-First Prompt Policy", RevisePlan.Render("feedback"), StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_prompts_define_the_structured_hitl_request_marker_rules()
    {
        Assert.Contains(
            "## HITL-Requested Non-Implementation Deliverables",
            WritePlan.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "documentation-centric milestones",
            WritePlan.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "only when each entry remains grounded in explicit HITL request evidence",
            RevisePlan.Render("feedback"),
            StringComparison.Ordinal);
    }

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
            Assert.Equal(AgentEffort.XHigh, spec.Effort);
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
    public async Task WritePlanAsync_CapturesStructuredHitlRequestMarkersFromTheWrittenPlan()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var rt = new FakeAgentRuntime(store);
        var ledger = new NonImplementationReviewLedgerStore(store);
        var capture = new ExplicitHitlNonImplementationRequestCaptureService(ledger);
        var session = new PlanSession(
            rt,
            artifacts,
            new RecordingLoopConsole(),
            repo,
            _hitlRequestCapture: capture);
        const string plan = """
            # Plan

            ## HITL-Requested Non-Implementation Deliverables

            | Path Or Pattern | Source | Source Hash | Rationale |
            | --- | --- | --- | --- |
            | docs/requested.md | user | abc | Human explicitly asked for the design note. |
            """;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), plan).Wait();
            return Turns.Completed("wrote plan");
        }));

        await session.WritePlanAsync(CancellationToken.None);

        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/requested.md", request.DeliverablePathOrPattern);
        Assert.Equal(OrchestrationArtifactPaths.Plan, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);
    }

    [Fact]
    public async Task WritePlanAsync_TurnFailed_ThrowsWithDiagnosticsTail_AndClosesSession()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "write-plan stderr tail")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
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

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => session.WritePlanAsync(CancellationToken.None));

        Assert.DoesNotContain("Agent stderr (tail):", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task WritePlanAsync_CompletedButNoPlanWritten_Throws_AndClosesSession()
    {
        var (session, rt, _, _, _) = New();
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
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

        await Assert.ThrowsAsync<PlanStepException>(() => session.WritePlanAsync(CancellationToken.None));

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

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
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

        await Assert.ThrowsAsync<PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

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

        await Assert.ThrowsAsync<PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task ReviseAsync_CalledWithoutPriorWritePlanAsync_Throws()
    {
        var (session, rt, _, _, _) = New();

        await Assert.ThrowsAsync<PlanStepException>(() => session.ReviseAsync("FEEDBACK", CancellationToken.None));

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

        await Assert.ThrowsAsync<PlanStepException>(() => session.WritePlanAsync(CancellationToken.None));
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

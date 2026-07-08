using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Cli.Tests.Services.Execution;
using LoopRelay.Cli.Tests.Services.Support;
using LoopRelay.Cli.Tests.Services.Usage;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Primitives;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public class DecisionSessionTests
{
    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con)
        New(DecisionSessionRouterOptions? routerOptions = null, IDecisionCostModel? costModel = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(routerOptions ?? new DecisionSessionRouterOptions());
        return (new DecisionSession(rt, router, art, con, repo, costModel), rt, store, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo,
        RecordingLoopConsole Con, FakeDecisionSessionResumeStore Resume)
        NewWithResume(
            DecisionSessionRouterOptions? routerOptions = null,
            DecisionSessionResumeState? state = null,
            bool resumeEnabled = true)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(routerOptions ?? new DecisionSessionRouterOptions());
        var resume = new FakeDecisionSessionResumeStore { State = state };
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null,
            _resumeStore: resume, _resumeEnabled: resumeEnabled);
        return (session, rt, store, repo, con, resume);
    }

    private static DecisionSessionResumeState ResumeState(string threadId = "thread-old") =>
        new(threadId, 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo,
        RecordingLoopConsole Con, FakeDecisionSessionResumeStore Resume, FakeProjectionService Projection)
        NewWithProjection(
            DecisionSessionRouterOptions? routerOptions = null,
            DecisionSessionResumeState? state = null,
            ProjectionFreshness? freshness = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(routerOptions ?? new DecisionSessionRouterOptions());
        var resume = new FakeDecisionSessionResumeStore { State = state };
        var projection = new FakeProjectionService("DECISION SESSION PROJECTION");
        if (freshness is not null)
        {
            projection.Freshness = freshness;
        }

        var session = new DecisionSession(
            rt,
            router,
            art,
            con,
            repo,
            _costModel: null,
            _resumeStore: resume,
            _projectionService: projection);
        return (session, rt, store, repo, con, resume, projection);
    }

    private const string ScopedContext = OrchestrationArtifactPaths.OperationalContext;
    private const string ScopedDelta = OrchestrationArtifactPaths.OperationalDelta;
    private const string ScopedPlan = OrchestrationArtifactPaths.Plan;
    private const string ScopedDetails = OrchestrationArtifactPaths.Details;

    [Fact]
    public async Task Run_FreshProcess_DeliversContextInline_Proposes_PersistsAndVerifiesDecisions()
    {
        var (session, rt, store, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // single proposal turn on the fresh process
        {
            // No separate seed turn (StartDecisionSession is gone): the fresh process is primed with the
            // operational context in THIS turn, and a prior handoff is present, so decisions.md is the NEXT
            // execution agent's system prompt (context + GenerateSystemPromptForNextExecutionAgent.Render(handoff)).
            Assert.Contains("OPCTX", prompt);
            Assert.Contains("HANDOFF", prompt);
            Assert.Contains("next execution agent", prompt);
            Assert.Contains("Repository growth is implementation-first", prompt);
            return Turns.Completed("DECISIONS-TEXT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Contains("DECISIONS-TEXT", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
    }

    [Fact]
    public async Task Run_CapturesStructuredHitlRequestMarkersFromPersistedDecisions()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new LoopArtifacts(store, repo);
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        var ledger = new NonImplementationReviewLedgerStore(store);
        var capture = new ExplicitHitlNonImplementationRequestCaptureService(ledger);
        var session = new DecisionSession(
            rt,
            router,
            artifacts,
            new RecordingLoopConsole(),
            repo,
            _hitlRequestCapture: capture);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        const string decisions = """
            Execute the next slice.

            ## HITL-Requested Non-Implementation Deliverables

            | Path Or Pattern | Source | Source Hash | Rationale |
            | --- | --- | --- | --- |
            | docs/requested.md | user | abc | Human explicitly requested the note. |
            """;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed(decisions)));

        await session.RunAsync(CancellationToken.None);

        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/requested.md", request.DeliverablePathOrPattern);
        Assert.Equal(OrchestrationArtifactPaths.Decisions, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);
    }

    [Fact]
    public async Task Run_FreshProcess_IncludesDecisionProjection()
    {
        var (session, rt, store, repo, _, _, projection) = NewWithProjection();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("DECISION SESSION PROJECTION", prompt);
            Assert.Contains("OPCTX", prompt);
            Assert.Contains("HANDOFF", prompt);
            Assert.Contains("next execution agent", prompt);
            return Turns.Completed("DECISIONS-TEXT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, projection.EnsureFreshCalls);
        Assert.Contains(ProjectionRuntimePromptNames.DecisionSession, projection.RuntimePromptNames);
    }

    [Fact]
    public async Task Run_WarmProcess_DoesNotResendDecisionProjection()
    {
        var (session, rt, store, repo, _, _, projection) = NewWithProjection();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("DECISION SESSION PROJECTION", prompt);
            return Turns.Completed("D1");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.DoesNotContain("DECISION SESSION PROJECTION", prompt);
            Assert.DoesNotContain("OPCTX", prompt);
            return Turns.Completed("D2");
        }));

        await session.RunAsync(CancellationToken.None);
        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, projection.EnsureFreshCalls);
    }

    [Fact]
    public async Task Run_FirstPass_NoHandoff_GeneratesFirstExecutionAgentSystemPrompt_PersistsDecisions()
    {
        var (session, rt, store, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // single proposal turn (fresh, no handoff)
        {
            // No handoff of any kind exists: this is the first pass, so decisions.md is the FIRST execution agent's
            // system prompt (GenerateSystemPromptForFirstExecutionAgent), with the operational context delivered
            // inline (no separate seed turn) — no throw.
            Assert.Contains("OPCTX", prompt);
            Assert.Contains("first execution agent", prompt);
            return Turns.Completed("FIRST-SYS-PROMPT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("FIRST-SYS-PROMPT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("FIRST-SYS-PROMPT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Contains("FIRST-SYS-PROMPT", con.Messages);
    }

    [Fact]
    public async Task Run_WhenOperationalContextEmpty_Warns()
    {
        var (session, rt, store, repo, con) = New();
        // Neither plan.md nor operational_context.md exists: EnsureOperationalContextAsync writes nothing, so the
        // operational-context read (when priming the fresh process) yields empty and the degraded condition is Warned.
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));   // single proposal turn

        await session.RunAsync(CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task Run_SecondRound_ReusesWarmSession_NoContextResend()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // round 1: fresh process, context primed inline
        {
            Assert.Contains("OPCTX", prompt);
            return Turns.Completed("D1");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // round 2: warm reuse — handoff delta only, NO context resend
        {
            Assert.DoesNotContain("OPCTX", prompt);
            return Turns.Completed("D2");
        }));

        await session.RunAsync(CancellationToken.None);
        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);     // warm reuse: only one process opened
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task Run_WhenProposeNotCompleted_ClosesSessionAndThrows()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task Dispose_ClosesWarmSession()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));

        await session.RunAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task Run_WhenOccupancyCrossesGuard_RecyclesViaTransfer()
    {
        // Small window (22 -> capacity guard round(22*0.9)=20): round 1 accrues occupancy 20, so round 2's routing
        // crosses the capacity guard and Transfers.
        var (session, rt, store, repo, con) = New(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose on the fresh process (accrues tokens so round 2 routes Transfer).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer => produce-delta (warm) + close + update-context (one-shot) + optimize-docs (one-shot)
        // + propose. The post-transfer proposal primes the fresh process with the just-evolved context inline — no
        // reseed turn.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) => Turns.Completed("DELTA-TEXT")));      // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                            // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated context");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));            // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                       // propose (context primed inline)
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("D2");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(4, rt.OpenSessions);   // original + two scoped artifact operations + recycled
        Assert.Equal(3, rt.ClosedSessions); // original + two scoped artifact operation sessions closed
    }

    [Fact]
    public async Task Run_EconomicMarginalRule_RecyclesViaTransfer()
    {
        // Economic transfer BELOW the capacity guard: a controllable cost model makes eNext >= (R+C)/n while
        // occupancy stays tiny. Proves the CLI's BuildRouterInputs/RecordProposalCost economic wiring (a hand
        // mirror of the orchestrator) actually produces a Transfer — not just the capacity-guard path.
        var costModel = new StubCostModel { MeasureValue = 300_000d, EstimateValue = 600_000d };
        var (session, rt, store, repo, _) = New(costModel: costModel); // default router options (256k window, marginal)
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose. Occupancy 20 (<< guard 230,400); R=300000, n=1.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: eNext 600000 >= (R 300000 + C 250000 default)/1 = 550000 -> economic Transfer (occupancy 20 << guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose (context inline)
        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(4, rt.OpenSessions);   // original + two scoped artifact operations + recycled
    }

    [Fact]
    public async Task Run_SubThresholdProposals_DoNotAccumulate_IntoTransfer()
    {
        // Sub-window reuse must NOT transfer under the default (250k-seeded) cost estimate: occupancy stays 20
        // (far below the guard), and the marginal rule's amortized average (R + C)/n is dominated by the 250k seed,
        // so the predicted next cost never crosses it. The conservative seed keeps reuse steady until a real
        // transfer measures C.
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H");

        // Round 1: propose (20 tokens).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: another sub-window proposal (occupancy 20). Reuse cost R accrues but stays far below the 250k seed.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D2", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 3: still Continue (the amortized average is dominated by the 250k seed). The extra turns below would
        // satisfy a (wrong) Transfer path (delta + update + optimize + propose), so a regression that transferred is
        // caught by the asserts, not a throw.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>                                     // Continue: proposal | Transfer: delta
            new AgentTurnResult(0, AgentTurnState.Completed, "D3", new AgentTokenUsage(10, 10))));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // Transfer-only: UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // Transfer-only: OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D3T")));           // Transfer-only: proposal
        await session.RunAsync(CancellationToken.None);

        // No transfer ever fired: no operational delta, the single warm process reused throughout, round-3 proposal kept.
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal("D3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task Run_Transfer_EvolvesInSandbox_ThenCopiesBackAndCleansUp()
    {
        // Stage 2 CLI mirror: the UpdateOperationalContext one-shot runs in an ISOLATED sandbox (distinct root, not
        // the repo), and the evolved context is copied back into the repo. Small window (guard 20) so round 2 transfers.
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer. The evolution one-shot writes the evolved context INSIDE the sandbox.
        string? seededContext = null;
        string? seededDelta = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            // The sandbox is seeded with ONLY the two evolution inputs; capture them before overwriting the context.
            seededContext = s.ReadAsync(Resolve(repo, ScopedContext)).Result;
            seededDelta = s.ReadAsync(Resolve(repo, ScopedDelta)).Result;
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));      // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                 // propose (context primed inline)
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("D2");
        }));
        await session.RunAsync(CancellationToken.None);

        AgentSessionSpec[] scopedSpecs = rt.OpenedSpecs
            .Where(spec => spec.OperationPermissionProfile is not null)
            .ToArray();
        Assert.Equal(2, scopedSpecs.Length);
        Assert.All(scopedSpecs, spec =>
        {
            Assert.Equal(repo.Path, spec.WorkingDirectory);
            Assert.Equal("read-only", spec.Sandbox.Identifier);
            Assert.True(spec.Sandbox.RequiresApproval);
        });
        Assert.Contains(scopedSpecs, spec => spec.OperationPermissionProfile!.Label == "operational-context-evolution");
        Assert.Contains(scopedSpecs, spec => spec.OperationPermissionProfile!.Label == "operational-documents-optimization");
        Assert.Empty(rt.OneShotCalls);

        // The sandbox was seeded with ONLY the two evolution inputs — the CURRENT repo context and the delta — so
        // codex folds the delta into the real base (not blank/wrong), and the delta seeding is not silently dropped.
        Assert.Equal("OPCTX-0", seededContext);
        Assert.Equal("DELTA-TEXT", seededDelta);

        // The evolved context was copied back into the repo; the delta was persisted to the repo too.
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));

        // No temp sandbox is created; rollback is handled by the artifact transaction.
    }

    [Fact]
    public async Task Run_RepeatedGrowingTransfers_WarnOnTheContextRatchet()
    {
        // Stage 2 CLI mirror of the size-health guard: three transfers producing a strictly larger operational
        // context ratchet the growth streak to the threshold and emit exactly ONE console warning (on the third).
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose at occupancy 22 (>= guard 20) so every subsequent round routes Transfer.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(11, 11))));
        await session.RunAsync(CancellationToken.None);

        // Three transfers producing 100/200/300-char contexts: baseline (no warn), +1 (no warn), +2 (WARN). Each
        // transfer is delta (warm) + update (one-shot) + optimize (one-shot, no-op — the size-health measurement
        // is taken on its copy-back) + propose (fresh, context primed inline) — no reseed turn.
        foreach (string context in new[] { new string('x', 100), new string('x', 200), new string('x', 300) })
        {
            string evolved = context;
            rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("delta")));       // ProduceOperationalDelta
            rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                    // UpdateOperationalContext
            {
                s.WriteAsync(Resolve(repo, ScopedContext), evolved).Wait();
                return Turns.Completed("updated");
            }));
            rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));    // OptimizeOperationalDocuments
            rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>                                    // propose (occupancy 22)
                new AgentTurnResult(0, AgentTurnState.Completed, "D", new AgentTokenUsage(11, 11))));
            await session.RunAsync(CancellationToken.None);
        }

        // Exactly one ratchet warning — on the third transfer (streak reaches the threshold of 2), not before.
        Assert.Equal(1, con.Events.Count(e => e.Kind == "warn" && e.Text.Contains("grown")));
    }

    [Fact]
    public async Task Run_Transfer_ArchivesTheDelta_AndRemovesTheLiveFile()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer (delta + update + optimize + propose; no reseed turn).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose
        await session.RunAsync(CancellationToken.None);

        // The delta was archived into .agents/deltas and the live file removed.
        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_FailedDeltaArchive_FailsTheTransfer()
    {
        var inner = new MemoryArtifactStore();
        var store = new ThrowOnDeltaArchiveStore(inner);
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));   // OptimizeOperationalDocuments
        // The propose turn is never reached because the archive throws first.

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

        // The context update succeeded before the archive failed.
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_OptimizesDocumentsInOwnSandbox_AndCopiesThemBack()
    {
        // The optimization one-shot runs immediately after the context evolution, in its OWN sandbox seeded with
        // plan.md + details.md + the JUST-EVOLVED operational_context.md (not the pre-transfer revision), and every
        // optimized document is copied back into the repo.
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory(); // distinct root (genuinely separate from the repo)
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer (delta + update + optimize + propose).
        string? seededPlan = null;
        string? seededDetails = null;
        string? seededContext = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // OptimizeOperationalDocuments
        {
            seededPlan = s.ReadAsync(Resolve(repo, ScopedPlan)).Result;
            seededDetails = s.ReadAsync(Resolve(repo, ScopedDetails)).Result;
            seededContext = s.ReadAsync(Resolve(repo, ScopedContext)).Result;
            s.WriteAsync(Resolve(repo, ScopedPlan), "PLAN-OPT").Wait();
            s.WriteAsync(Resolve(repo, ScopedDetails), "DETAILS-OPT").Wait();
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-OPT").Wait();
            return Turns.Completed("optimized");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                 // propose (context primed inline)
        {
            // The fresh process is primed with the OPTIMIZED context — the revision the transfer left in the repo.
            Assert.Contains("OPCTX-OPT", prompt);
            return Turns.Completed("D2");
        }));
        await session.RunAsync(CancellationToken.None);

        // The optimization sandbox was seeded with the plan, the details, and the just-evolved context.
        Assert.Equal("PLAN-0", seededPlan);
        Assert.Equal("DETAILS-0", seededDetails);
        Assert.Equal("OPCTX-1", seededContext);

        AgentSessionSpec optimizationSpec = rt.OpenedSpecs
            .Where(spec => spec.OperationPermissionProfile is not null)
            .ElementAt(1);
        Assert.Equal("operational-documents-optimization", optimizationSpec.OperationPermissionProfile!.Label);
        Assert.Equal(repo.Path, optimizationSpec.WorkingDirectory);
        Assert.Empty(rt.OneShotCalls);

        // All three optimized documents were copied back into the repo.
        Assert.Equal("PLAN-OPT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.Equal("DETAILS-OPT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.Equal("OPCTX-OPT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_OptimizeWithoutOptionalDocuments_SeedsAndCopiesBackOnlyPresentOnes()
    {
        // details.md (and even plan.md) are optional inputs to the optimization: absent documents are not seeded
        // into the sandbox and are not conjured into the repo on copy-back — only operational_context.md is required.
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        bool? sandboxHadPlan = null;
        bool? sandboxHadDetails = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // OptimizeOperationalDocuments
        {
            sandboxHadPlan = s.ExistsAsync(Resolve(repo, ScopedPlan)).Result;
            sandboxHadDetails = s.ExistsAsync(Resolve(repo, ScopedDetails)).Result;
            return Turns.Completed("optimized");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));             // propose
        await session.RunAsync(CancellationToken.None);

        Assert.False(sandboxHadPlan);
        Assert.False(sandboxHadDetails);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_FailedOptimize_FailsTheTransfer_BeforeTheDeltaArchive()
    {
        // A non-completed optimization turn fails the transfer (hard step, mirroring the evolution): the delta is
        // never archived and the repo keeps the EVOLVED context the update already copied back.
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));                    // OptimizeOperationalDocuments
        // Neither the archive nor the propose turn is reached.

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_EmitsProposePhase_AfterTheTransferPhases()
    {
        // Console visibility: every decision run announces its proposal turn with its own phase header, so
        // post-transfer proposal output no longer prints under the last "Decision: Transfer/…" header.
        var (session, rt, store, repo, con) = New(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer (delta + update + optimize + archive), then the proposal.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose
        await session.RunAsync(CancellationToken.None);

        List<string> phases = con.Events.Where(e => e.Kind == "phase").Select(e => e.Text).ToList();
        // One "Decision: Propose" per decision run (round 1 and round 2)…
        Assert.Equal(2, phases.Count(p => p == "Decision: Propose"));
        // …and the post-transfer proposal's header comes AFTER the last transfer phase, closing it out.
        int archive = phases.LastIndexOf("Decision: Transfer/ArchiveOperationalDelta");
        Assert.True(archive >= 0);
        Assert.True(phases.LastIndexOf("Decision: Propose") > archive);
        Assert.Equal("Decision: Propose", phases[^1]);
    }

    [Fact]
    public async Task Run_Transfer_UnchangedEvolution_FailsBeforeTheDeltaIsArchived()
    {
        // The unchanged-context guard: an evolution one-shot that "completes" without touching the seeded
        // sandbox context is exactly what a silently-failed codex launch looks like (the CLI seeded the
        // file itself, so a bare existence check is self-satisfied). The transfer must FAIL before the
        // delta archive, so the un-applied delta stays live for a retry instead of being consumed.
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, _costModel: null);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));  // UpdateOperationalContext: no write
        // Neither the optimize one-shot, nor the archive, nor the propose turn is reached.

        LoopStepException ex = await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Contains("unchanged", ex.Message);

        // The delta was NOT consumed: still live, nothing rotated into .agents/deltas/.
        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        // The repo context is untouched (the unevolved sandbox copy was never copied back).
        Assert.Equal("OPCTX-0", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
        // Only the evolution scoped operation ran.
        Assert.Single(rt.OpenedSpecs, spec => spec.OperationPermissionProfile is not null);
    }

    [Fact]
    public async Task Run_Transfer_FailedUpdate_SurfacesTheAgentStderrInTheThrownMessage()
    {
        // A failed one-shot's Diagnostics (the codex process's retained stderr tail) must reach the
        // operator through the LoopStepException message — a bare turn state hides WHY codex refused.
        var (session, rt, store, repo, _) = New(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>                                     // UpdateOperationalContext fails
            Turns.Failed("", "Not inside a trusted directory and --skip-git-repo-check was not specified.")));

        LoopStepException ex = await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Contains("Not inside a trusted directory", ex.Message);
    }

    [Fact]
    public async Task Run_WhenProposeFails_TheThrownMessageCarriesDiagnostics()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "codex stderr tail")));

        LoopStepException ex = await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Contains("codex stderr tail", ex.Message);
    }

    [Fact]
    public void UpdateOperationalContextPrompt_InstructsFileWrite_NotOutputCapture()
    {
        // The evolution flow (CLI and backend, both flag paths) reads .agents/operational_context.md back from the
        // workspace and DISCARDS the turn output — one-shot Output concatenates every agent message, preambles
        // included, so it can never be consumed as the document. A prompt that asks for the replacement document as
        // OUTPUT makes the agent legitimately skip the write and trip the unchanged-context guard (observed live
        // 2026-07-02: the agent read both seeded files and replied with the merged doc, never touching the file).
        Assert.Contains("overwriting `.agents/operational_context.md`", UpdateOperationalContext.Text);
        Assert.DoesNotContain("The output should be the complete replacement document", UpdateOperationalContext.Text);
    }

    [Fact]
    public async Task Run_FirstEntry_WithPersistedState_ResumesWarm_NoContextResend_AndRestoresAccounting()
    {
        var (session, rt, store, repo, con, resume) = NewWithResume(state: ResumeState());
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            // A successfully resumed thread already holds the operational context — the proposal is the
            // warm handoff-only delta, exactly as if the process had never restarted.
            Assert.DoesNotContain("OPCTX", prompt);
            Assert.Contains("HANDOFF", prompt);
            return Turns.Completed("D-RESUMED");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("thread-old", rt.OpenedSpecs.Single().ResumeThreadId);
        Assert.Contains(con.Events, e => e.Kind == "info" && e.Text.Contains("Resumed decision session"));
        // The restored accounting flowed through the post-turn persist: reuseCycles 2 -> 3, reuseCost intact,
        // transfer calibration intact.
        DecisionSessionResumeState written = Assert.Single(resume.Written);
        Assert.Equal("thread-old", written.ThreadId);
        Assert.Equal(3, written.ReuseCycles);
        Assert.Equal(5d, written.ReuseCost);
        Assert.Equal(300_000d, written.TransferCost);
        Assert.Equal(1, written.TransferCount);
    }

    [Fact]
    public async Task Run_FirstEntry_WithStaleDecisionProjection_ClearsResumeAndStartsFresh()
    {
        var (session, rt, store, repo, con, resume, projection) = NewWithProjection(
            state: ResumeState(),
            freshness: ProjectionFreshness.Stale(ProjectionStaleReason.ProjectContextDrift));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("DECISION SESSION PROJECTION", prompt);
            Assert.Contains("OPCTX", prompt);
            Assert.Contains("HANDOFF", prompt);
            return Turns.Completed("D-FRESH");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, projection.EvaluateFreshnessCalls);
        Assert.Equal(1, resume.ClearCalls);
        Assert.Null(rt.OpenedSpecs.Single().ResumeThreadId);
        Assert.Contains(con.Events, e => e.Kind == "warn" && e.Text.Contains("projection is stale or missing"));
    }

    [Fact]
    public async Task Run_FirstEntry_ResumeFails_WarnsClearsAndFallsBackToAFreshPrimedProcess()
    {
        var (session, rt, store, repo, con, resume) = NewWithResume(state: ResumeState());
        rt.FailResume = true;
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("OPCTX", prompt);   // fresh process: primed inline, byte-identical to today
            return Turns.Completed("D-FRESH");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal(2, rt.OpenedSpecs.Count);
        Assert.Equal("thread-old", rt.OpenedSpecs[0].ResumeThreadId);
        Assert.Null(rt.OpenedSpecs[1].ResumeThreadId);
        Assert.Contains(con.Events, e => e.Kind == "warn" && e.Text.Contains("Could not resume"));
        Assert.Equal(1, resume.ClearCalls);
        // The fresh thread re-persisted after its successful turn — the next run resumes THAT thread.
        Assert.Equal("thread-1", Assert.Single(resume.Written).ThreadId);
    }

    [Fact]
    public async Task Run_NoPersistedState_OpensFresh_AndPersistsAfterTheSuccessfulProposal()
    {
        var (session, rt, store, repo, _, resume) = NewWithResume();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));

        await session.RunAsync(CancellationToken.None);

        Assert.Null(rt.OpenedSpecs.Single().ResumeThreadId);
        DecisionSessionResumeState written = Assert.Single(resume.Written);
        Assert.Equal("thread-1", written.ThreadId);
        Assert.Equal(1, written.ReuseCycles);
        Assert.Equal(20, written.OccupancyTokens);
    }

    [Fact]
    public async Task Run_TransferRecycle_ClearsTheState_ReopensWithoutResume_ThenPersistsTheNewThread()
    {
        var (session, rt, store, repo, _, resume) = NewWithResume(
            new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard and Transfers).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer (delta + update + optimize + propose on the recycled process).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));
        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, resume.ClearCalls);                    // the transfer close deleted the dead thread's state
        Assert.Null(rt.OpenedSpecs[1].ResumeThreadId);         // the recycle opened FRESH — resume is first-open-only
        Assert.Equal(2, resume.Written.Count);
        Assert.Equal("thread-4", resume.Written[^1].ThreadId); // the post-transfer thread re-persisted
    }

    [Fact]
    public async Task Run_TransferRecycle_ReinjectsDecisionProjectionOnFreshProcess()
    {
        var (session, rt, store, repo, _, _, projection) = NewWithProjection(
            new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("DECISION SESSION PROJECTION", prompt);
            return new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10));
        }));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, ScopedContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("DECISION SESSION PROJECTION", prompt);
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("D2");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal(2, projection.EnsureFreshCalls);
    }

    [Fact]
    public async Task Run_FailedProposal_ClearsThePersistedState()
    {
        var (session, rt, store, repo, _, resume) = NewWithResume();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

        Assert.Equal(1, resume.ClearCalls);
        Assert.Empty(resume.Written);
    }

    [Fact]
    public async Task Dispose_KeepsThePersistedState_ItIsTheNextRunsResumePayload()
    {
        var (session, rt, store, repo, _, resume) = NewWithResume();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));

        await session.RunAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.Equal(0, resume.ClearCalls);
        Assert.NotNull(resume.State);
        Assert.Equal(1, rt.ClosedSessions);   // the process still dies with the run — only the STATE survives
    }

    [Fact]
    public async Task Run_WhenResumeDisabled_OpensFresh_ButStillPersists()
    {
        var (session, rt, store, repo, _, resume) = NewWithResume(state: ResumeState(), resumeEnabled: false);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            Assert.Contains("OPCTX", prompt);   // no resume attempt -> fresh priming
            return Turns.Completed("D1");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Null(rt.OpenedSpecs.Single().ResumeThreadId);   // the kill switch skips ONLY the resume attempt
        Assert.NotEmpty(resume.Written);                        // persist/clear behavior is unchanged
    }
}

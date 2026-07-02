using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

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
        // These tests are not about sandbox isolation — root the Stage-2 sandbox at the repo so the in-place rewrite
        // scripts resolve transparently (see FakeSandboxWorkspaceFactory). The dedicated isolation test uses a
        // distinct root.
        var sandbox = new FakeSandboxWorkspaceFactory { Root = repo.Path };
        return (new DecisionSession(rt, router, art, con, repo, costModel, sandbox), rt, store, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

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
            return Turns.Completed("DECISIONS-TEXT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Contains("DECISIONS-TEXT", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
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
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                            // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated context");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));            // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                       // propose (context primed inline)
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("D2");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(2, rt.OpenSessions);   // original + recycled
        Assert.Equal(1, rt.ClosedSessions); // original closed during recycle
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
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose (context inline)
        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(2, rt.OpenSessions);   // original + recycled (economic transfer)
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
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // Transfer-only: UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // Transfer-only: OptimizeOperationalDocuments
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
        var sandbox = new FakeSandboxWorkspaceFactory(); // distinct root (genuinely separate from the repo)
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

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
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            // The sandbox is seeded with ONLY the two evolution inputs; capture them before overwriting the context.
            seededContext = s.ReadAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext)).Result;
            seededDelta = s.ReadAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalDelta)).Result;
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));      // OptimizeOperationalDocuments
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                 // propose (context primed inline)
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("D2");
        }));
        await session.RunAsync(CancellationToken.None);

        // Both transfer one-shots (evolution, then optimization) ran against the SANDBOX root, not the repository
        // working directory.
        Assert.Equal(2, rt.OneShotCalls.Count);
        Assert.All(rt.OneShotCalls, call =>
        {
            Assert.Equal(sandbox.Root, call.Spec.WorkingDirectory);
            Assert.NotEqual(repo.Path, call.Spec.WorkingDirectory);
        });

        // The sandbox was seeded with ONLY the two evolution inputs — the CURRENT repo context and the delta — so
        // codex folds the delta into the real base (not blank/wrong), and the delta seeding is not silently dropped.
        Assert.Equal("OPCTX-0", seededContext);
        Assert.Equal("DELTA-TEXT", seededDelta);

        // The evolved context was copied back into the repo; the delta was persisted to the repo too.
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));

        // One sandbox per one-shot (evolution + optimization), each disposed (cleaned up).
        Assert.Equal(2, sandbox.CreatedCount);
        Assert.Equal(2, sandbox.Disposed.Count);
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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

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
            rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                    // UpdateOperationalContext
            {
                s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), evolved).Wait();
                return Turns.Completed("updated");
            }));
            rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));    // OptimizeOperationalDocuments
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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer (delta + update + optimize + propose; no reseed turn).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));     // OptimizeOperationalDocuments
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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));   // OptimizeOperationalDocuments
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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

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
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // OptimizeOperationalDocuments
        {
            seededPlan = s.ReadAsync(sandbox.Resolve(OrchestrationArtifactPaths.Plan)).Result;
            seededDetails = s.ReadAsync(sandbox.Resolve(OrchestrationArtifactPaths.Details)).Result;
            seededContext = s.ReadAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext)).Result;
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.Plan), "PLAN-OPT").Wait();
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.Details), "DETAILS-OPT").Wait();
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-OPT").Wait();
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

        // The optimization ran as its own one-shot, against the sandbox root, with the optimization prompt.
        Assert.Equal(2, rt.OneShotCalls.Count);
        Assert.Equal(sandbox.Root, rt.OneShotCalls[1].Spec.WorkingDirectory);
        Assert.Equal(OptimizeOperationalDocuments.Text, rt.OneShotCalls[1].Prompt);

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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        bool? sandboxHadPlan = null;
        bool? sandboxHadDetails = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // OptimizeOperationalDocuments
        {
            sandboxHadPlan = s.ExistsAsync(sandbox.Resolve(OrchestrationArtifactPaths.Plan)).Result;
            sandboxHadDetails = s.ExistsAsync(sandbox.Resolve(OrchestrationArtifactPaths.Details)).Result;
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
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));     // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                      // UpdateOperationalContext
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));                    // OptimizeOperationalDocuments
        // Neither the archive nor the propose turn is reached.

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }
}

/// <summary>Forwards to an inner store but throws when a write targets the .agents/deltas archive — models a
/// failed delta archive so the strict transfer-fail path can be exercised.</summary>
internal sealed class ThrowOnDeltaArchiveStore(IArtifactStore inner) : IArtifactStore
{
    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);
    public Task<string?> ReadAsync(string path) => inner.ReadAsync(path);
    public Task WriteAsync(string path, string content) =>
        path.Replace('\\', '/').Contains("/deltas/", StringComparison.OrdinalIgnoreCase)
            ? throw new IOException("Configured archive write failure.")
            : inner.WriteAsync(path, content);
    public Task DeleteAsync(string path) => inner.DeleteAsync(path);
    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) => inner.ListAsync(path, searchPattern);
    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}

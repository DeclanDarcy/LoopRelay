using System.Linq;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using CommandCenter.Backend.Tests.Orchestration;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// m8 "Contracts, Artifacts, Provenance" — the provenance certification: prompt provenance is attached to
/// planning, execution, decision, AND transfer turns, and EVERY recorded entry carries all seven fields
/// (PromptName, PromptType, SourceHash, SessionRole, WorkflowPhase, InputArtifactIdentities,
/// OutputArtifactIdentities). Driven through a full lifecycle that INCLUDES a Transfer route so the three
/// transfer turns actually run.
///
/// Two artifact-identity sets are INTENTIONALLY empty by design and are asserted explicitly as part of the
/// contract (not treated as gaps):
///   - StartDecisionSession (seed) and StartDecisionSessionFromTransfer: EMPTY OutputArtifactIdentities
///     (a seed produces no artifact; it primes the in-process conversation).
///   - ProduceOperationalDelta: EMPTY InputArtifactIdentities (it renders no files; it extracts the delta
///     from the in-process Decision conversation).
/// </summary>
public sealed class OrchestrationProvenanceCertificationTests
{
    private const string Plan = "PLAN TEXT";
    private const string OperationalContext = "OPERATIONAL CONTEXT";
    private const string FirstHandoff = "HANDOFF ONE";

    [Fact]
    public async Task A_full_transfer_lifecycle_attaches_complete_provenance_to_every_turn()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator =
            OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router);

        // --- plan/write -> .agents/plan.md (planning provenance: WritePlan) ---
        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await orchestrator.BeginWritePlanAsync(
            repository,
            new PlanWriteRequest { Epic = "EPIC", Specs = new[] { "SPEC ONE" } });
        await orchestrator.PlanningTurnTask;
        runtime.OnTurn = null;

        // --- plan/execute -> ExtractMilestones + StartExecution (execution provenance) ---
        ScriptMilestoneExtraction(runtime, store, repository, "m1.md");
        ScriptStartExecution(runtime, store, repository, FirstHandoff);
        await orchestrator.BeginExecutePlanAsync(repository);
        await orchestrator.ExecutionRunTask;

        // --- decision/run (seed + GetNextDecisions) -> primes the warm process (transfer eligibility) ---
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());                          // StartDecisionSession seed
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "SEED DECISIONS"));  // GetNextDecisions
        await orchestrator.BeginDecisionRunAsync(repository, DecisionRoute.Continue);
        await orchestrator.DecisionRunTask;

        // --- decision/submit -> continuation (ContinueExecution) THEN a Transfer-route decision run ---
        // Continuation one-shot writes the next live handoff; transfer rewrite one-shot writes operational_context.
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF TWO")));
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext), "REWRITTEN CONTEXT")));
        // Transfer session turns: delta extraction, reseed-from-transfer, proposal.
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "OPERATIONAL DELTA"));
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn());
        runtime.SessionTurns.Enqueue(new FakeOneShotTurn(Output: "NEXT DECISIONS"));

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // ---------------------------------------------------------------------------------------------------
        // PLANNING provenance — a WritePlan entry.
        // ---------------------------------------------------------------------------------------------------
        PromptProvenance writePlan = Assert.Single(
            orchestrator.PlanningProvenance, p => p.WorkflowPhase == "WritePlan");
        AssertAllSevenFields(writePlan, PromptSessionRole.Planning, "WritePlan");
        // WritePlan renders epic + spec inputs and is directed to produce the plan.
        Assert.Contains(OrchestrationArtifactPaths.SpecsEpic, writePlan.InputArtifactIdentities);
        Assert.Contains(OrchestrationArtifactPaths.Spec(1), writePlan.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(writePlan.OutputArtifactIdentities));

        // ---------------------------------------------------------------------------------------------------
        // EXECUTION provenance — ExtractMilestones, StartExecution, ContinueExecution.
        // ---------------------------------------------------------------------------------------------------
        PromptProvenance extract = Single(orchestrator.ExecutionProvenance, nameof(ExtractMilestones));
        AssertAllSevenFields(extract, PromptSessionRole.OperationalExecution, "ExtractMilestones");
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.OperationalContext },
            extract.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.MilestonesDirectory, Assert.Single(extract.OutputArtifactIdentities));

        PromptProvenance start = Single(orchestrator.ExecutionProvenance, nameof(StartExecution));
        AssertAllSevenFields(start, PromptSessionRole.OperationalExecution, "StartExecution");
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(start.InputArtifactIdentities));
        Assert.Equal(OrchestrationArtifactPaths.LiveHandoff, Assert.Single(start.OutputArtifactIdentities));

        PromptProvenance continueExec = Single(orchestrator.ExecutionProvenance, nameof(ContinueExecution));
        AssertAllSevenFields(continueExec, PromptSessionRole.OperationalExecution, "ContinueExecution");
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.HistoricalHandoff(1), OrchestrationArtifactPaths.Decisions },
            continueExec.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.LiveHandoff, Assert.Single(continueExec.OutputArtifactIdentities));

        // ---------------------------------------------------------------------------------------------------
        // DECISION provenance — StartDecisionSession (seed), GetNextDecisions, and the THREE transfer turns.
        // ---------------------------------------------------------------------------------------------------
        PromptProvenance seed = Single(orchestrator.DecisionProvenance, nameof(StartDecisionSession));
        AssertAllSevenFields(seed, PromptSessionRole.Decision, "StartDecisionSession");
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(seed.InputArtifactIdentities));
        Assert.Empty(seed.OutputArtifactIdentities); // INTENTIONAL: a seed produces no artifact.

        PromptProvenance getNext = orchestrator.DecisionProvenance.First(p => p.PromptName == nameof(GetNextDecisions));
        AssertAllSevenFields(getNext, PromptSessionRole.Decision, "GetNextDecisions");
        Assert.Single(getNext.InputArtifactIdentities); // the handoff it reasons over
        Assert.Equal(OrchestrationArtifactPaths.Decisions, Assert.Single(getNext.OutputArtifactIdentities));

        PromptProvenance delta = Single(orchestrator.DecisionProvenance, nameof(ProduceOperationalDelta));
        AssertAllSevenFields(delta, PromptSessionRole.Transfer, "ProduceOperationalDelta");
        Assert.Empty(delta.InputArtifactIdentities); // INTENTIONAL: extracts from the in-process conversation.
        Assert.Equal(OrchestrationArtifactPaths.OperationalDelta, Assert.Single(delta.OutputArtifactIdentities));

        PromptProvenance rewrite = Single(orchestrator.DecisionProvenance, nameof(UpdateOperationalContext));
        AssertAllSevenFields(rewrite, PromptSessionRole.ContextUpdate, "UpdateOperationalContext");
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.OperationalContext, OrchestrationArtifactPaths.OperationalDelta },
            rewrite.InputArtifactIdentities);
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(rewrite.OutputArtifactIdentities));

        PromptProvenance reseed = Single(orchestrator.DecisionProvenance, nameof(StartDecisionSessionFromTransfer));
        AssertAllSevenFields(reseed, PromptSessionRole.Transfer, "StartDecisionSessionFromTransfer");
        Assert.Equal(OrchestrationArtifactPaths.OperationalContext, Assert.Single(reseed.InputArtifactIdentities));
        Assert.Empty(reseed.OutputArtifactIdentities); // INTENTIONAL: a reseed produces no artifact.

        // The transfer-triggered proposal (the LAST GetNextDecisions) is recorded too.
        PromptProvenance transferProposal = orchestrator.DecisionProvenance.Last(p => p.PromptName == nameof(GetNextDecisions));
        AssertAllSevenFields(transferProposal, PromptSessionRole.Decision, "GetNextDecisions");
    }

    [Fact]
    public async Task Revise_plan_attaches_complete_provenance_with_a_revise_workflow_phase()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store);

        runtime.OnTurn = () => store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.Plan), Plan);
        await orchestrator.BeginWritePlanAsync(repository, new PlanWriteRequest { Epic = "r" });
        await orchestrator.PlanningTurnTask;

        await orchestrator.BeginRevisePlanAsync(repository, new PlanReviseRequest { Feedback = "tighten scope" });
        await orchestrator.PlanningTurnTask;

        PromptProvenance revise = Assert.Single(
            orchestrator.PlanningProvenance, p => p.WorkflowPhase == "RevisePlan");
        AssertAllSevenFields(revise, PromptSessionRole.Planning, "RevisePlan");
        Assert.Equal(nameof(RevisePlan), revise.PromptName);
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(revise.InputArtifactIdentities));
        Assert.Equal(OrchestrationArtifactPaths.Plan, Assert.Single(revise.OutputArtifactIdentities));
    }

    // ---- helpers ----

    /// <summary>
    /// Certifies the contract for a single recorded entry: all seven fields present, with the four scalar
    /// fields non-empty (the two artifact-identity sets are asserted per-entry by the caller, since some are
    /// intentionally empty). SessionRole and WorkflowPhase are pinned to their expected values.
    /// </summary>
    private static void AssertAllSevenFields(
        PromptProvenance provenance,
        PromptSessionRole expectedRole,
        string expectedWorkflowPhase)
    {
        Assert.False(string.IsNullOrWhiteSpace(provenance.PromptName), "PromptName must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(provenance.PromptType), "PromptType must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(provenance.SourceHash), "SourceHash must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(provenance.WorkflowPhase), "WorkflowPhase must be non-empty");
        Assert.Equal(expectedRole, provenance.SessionRole);
        Assert.Equal(expectedWorkflowPhase, provenance.WorkflowPhase);
        // The two artifact-identity collections are always non-null (the contract; emptiness is per-entry).
        Assert.NotNull(provenance.InputArtifactIdentities);
        Assert.NotNull(provenance.OutputArtifactIdentities);
        // PromptType is the catalog type's full name (CommandCenter.Core.Prompts.*).
        Assert.StartsWith("CommandCenter.Core.Prompts.", provenance.PromptType);
    }

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private static PromptProvenance Single(IReadOnlyList<PromptProvenance> provenance, string promptName) =>
        provenance.Single(p => p.PromptName == promptName);

    private static void ScriptMilestoneExtraction(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        params string[] milestoneFileNames) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: async () =>
        {
            foreach (string name in milestoneFileNames)
            {
                await store.WriteAsync(Resolve(repository, $"{OrchestrationArtifactPaths.MilestonesDirectory}/{name}"), "milestone");
            }
        }));

    private static void ScriptStartExecution(
        FakeAgentRuntime runtime,
        FakeArtifactStore store,
        Repository repository,
        string handoff) =>
        runtime.OneShotTurns.Enqueue(new FakeOneShotTurn(Effect: () =>
            store.WriteAsync(Resolve(repository, OrchestrationArtifactPaths.LiveHandoff), handoff)));
}

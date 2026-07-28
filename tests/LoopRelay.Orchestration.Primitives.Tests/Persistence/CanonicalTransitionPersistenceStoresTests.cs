using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalTransitionPersistenceStoresTests
{
    [Fact]
    public async Task Recommendation_and_policy_evaluation_round_trip_as_separate_causal_facts()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        var recommendationStore = new CanonicalExecutionRecommendationEvidenceStore(persistence);
        var evaluationStore = new CanonicalRuntimeProfileEvaluationStore(persistence);
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        var recommendation = new ExecutionRecommendationEvidence(
            ExecutionRecommendationIdentity.New(), decision, causality,
            AgentSessionIdentity.New(), TurnIdentity.New(), AgentModel.Gpt56Terra,
            AgentEffort.High, "Prefer the balanced execution model.", DateTimeOffset.UtcNow);
        await recommendationStore.AppendAsync(recommendation);
        var profile = new ResolvedRuntimeProfile(
            new RuntimeProfileIdentity("runtime-test"), "codex", AgentModel.Gpt56Terra,
            AgentEffort.High, "persistent", "danger-full-access", "execution",
            "never", "resume", TimeSpan.FromMinutes(10), "default", "reconcile");
        var evaluation = new RuntimeProfileEvaluation(
            RuntimeProfileEvaluationIdentity.New(), recommendation.Identity, decision,
            new PolicyIdentity("policy-test"), Capabilities(),
            RuntimeProfileEvaluationOutcome.Accepted, profile, ["allowed"], DateTimeOffset.UtcNow);
        await evaluationStore.AppendAsync(evaluation);

        ExecutionRecommendationEvidence readRecommendation =
            Assert.IsType<ExecutionRecommendationEvidence>(await recommendationStore.ReadAsync(recommendation.Identity));
        ExecutionRecommendationEvidence latestRecommendation =
            Assert.IsType<ExecutionRecommendationEvidence>(await recommendationStore.ReadLatestAsync());
        RuntimeProfileEvaluation readEvaluation =
            Assert.IsType<RuntimeProfileEvaluation>(await evaluationStore.ReadAsync(evaluation.Identity));
        ResolvedRuntimeProfile readProfile = Assert.IsType<ResolvedRuntimeProfile>(
            await ((IResolvedRuntimeProfileStore)evaluationStore).ReadAsync(profile.Identity));

        Assert.Equal(decision, readRecommendation.DecisionProduct);
        Assert.Equal(recommendation.Identity, latestRecommendation.Identity);
        Assert.Equal(causality.Attempt, readRecommendation.SourceCausality.Attempt);
        Assert.Equal(recommendation.Identity, readEvaluation.Recommendation);
        Assert.Equal(profile.Identity, readProfile.Identity);
    }

    private static ProviderCapabilityEvidence Capabilities() => new(
        ProviderCapabilityEvidenceIdentity.New(), "codex",
        Enum.GetValues<AgentModel>(), AgentEffort.XHigh, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Candidate_registration_is_non_promoting_and_bound_to_the_attempt()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);

        await new CanonicalCandidateProductStore(persistence)
            .RegisterAsync(causality, [Product(causality)], CancellationToken.None);

        ProductRecord candidate = Assert.Single((await persistence.LoadSnapshotAsync()).Products);
        Assert.Equal(ProductLifecycle.Proposed, candidate.Lifecycle);
        Assert.Equal(ProductValidationState.Unknown, candidate.ValidationState);
        Assert.Equal(ProductFreshness.Unknown, candidate.Freshness);
        Assert.Equal(causality.Attempt.Value, candidate.CausalIdentity);
    }

    [Fact]
    public async Task Run_and_prompt_facts_round_trip_with_full_causal_identity()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        TransitionRuntimeRequest request = Request(causality);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        var runs = new CanonicalTransitionRunStore(persistence);

        await runs.PersistStartedAsync(new TransitionRunStarted(
            causality,
            DateTimeOffset.UtcNow,
            request,
            definition,
            new TransitionInputSnapshot("snapshot", [], new Dictionary<string, string>(), []),
            prompt), CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        CanonicalTransitionRunRecord stored = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(causality.TransitionRun.Value, stored.RunId);
        Assert.Equal("snapshot", stored.InputSnapshotHash);
        CanonicalRenderedPromptRecord rendered = Assert.Single(await persistence.ReadRenderedPromptsAsync());
        Assert.Equal(causality.Attempt.Value, rendered.AttemptId);
        Assert.Equal(prompt.Fact.ContentHash, rendered.RenderedSha256);
    }

    [Fact]
    public async Task Atomic_commit_promotes_products_completes_attempt_and_enqueues_effect_intent()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        TransitionRuntimeRequest request = Request(causality);
        WorkflowTransitionDefinition definition = Definition(withEffect: true);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        await new CanonicalTransitionRunStore(persistence).PersistStartedAsync(
            new TransitionRunStarted(
                causality,
                DateTimeOffset.UtcNow,
                request,
                definition,
                new TransitionInputSnapshot("snapshot", [], new Dictionary<string, string>(), []),
                prompt),
            CancellationToken.None);
        ProductRecord product = Product(causality);
        ProductValidationResult validation = new(
            ProductValidationStatus.Valid, [product], [], [], [], [], "valid", ["validator"]);
        GateResult outputGate = new(GateStatus.Satisfied, [], "satisfied", ["gate"]);

        await new CanonicalTransitionCommitStore(persistence).CommitAsync(
            new TransitionCommitCapture(
                causality,
                request,
                definition,
                validation,
                outputGate,
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(product.Identity, Assert.Single(snapshot.Products).Identity);
        Assert.Equal(TransitionDurableState.EffectsPending, Assert.Single(snapshot.TransitionRuns).State);
        Assert.Empty(snapshot.EffectRecords);
        EffectScanRow work = Assert.Single(await new CanonicalEffectWorkStore(repository)
            .ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(EffectLifecycle.Planned, work.State);
        Assert.Equal("EffectsPending", Assert.Single(await persistence.ReadAttemptsAsync()).Outcome);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM canonical_effect_intents;";
        Assert.Equal("Planned", Convert.ToString(await command.ExecuteScalarAsync()));

        var settlement = new CanonicalEffectPlanSettlementStore(repository);
        Assert.False(await settlement.TrySettleAsync(causality.TransitionRun, CancellationToken.None));
        var effectStore = new CanonicalEffectWorkStore(repository);
        await effectStore.RecordReceiptAsync(
            work.Intent.Identity,
            new EffectReceipt(
                EffectReceiptIdentity.New(), work.Intent.Identity, work.Intent.Executor,
                work.Intent.ExecutorVersion, work.Intent.Target.Identity, "before", "after",
                true, "test:effect", ["independent-observation"], DateTimeOffset.UtcNow),
            "test-worker",
            CancellationToken.None);

        snapshot = await persistence.LoadSnapshotAsync();
        CanonicalWorkflowStateRecord pendingWorkflow = Assert.Single(snapshot.WorkflowStates);
        Assert.Equal(WorkflowResolutionState.Active, pendingWorkflow.State);
        Assert.Equal(RuntimeOutcomeKind.EffectsPending, pendingWorkflow.Outcome);
        Assert.True(await settlement.TrySettleAsync(causality.TransitionRun, CancellationToken.None));

        snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(TransitionDurableState.Completed, Assert.Single(snapshot.TransitionRuns).State);
        Assert.Equal("Completed", Assert.Single(await persistence.ReadAttemptsAsync()).Outcome);
        CanonicalWorkflowStateRecord settledWorkflow = Assert.Single(snapshot.WorkflowStates);
        Assert.Equal(WorkflowResolutionState.Resumable, settledWorkflow.State);
        Assert.Equal(RuntimeOutcomeKind.Waiting, settledWorkflow.Outcome);
    }

    /// <summary>
    /// Stage routing out of `InterpretCompletionRoute` follows the typed `CompletionRouteDecided`
    /// fact the transition recorded, and nothing else. The raw prompt output seeded here is
    /// deliberately prose with no decision row in it, so a router that had gone back to reading the
    /// rendered text could not pass by accident - it would find nothing to read.
    /// </summary>
    [Theory]
    [InlineData(true, "Workflow Completion")]
    [InlineData(false, "Execution Readiness")]
    public async Task Completion_routing_follows_the_typed_decision_fact_not_the_rendered_output(
        bool shouldCloseEpic,
        string expectedStage)
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCompletionRouteRunAsync(persistence);
        await RecordRenderedOutputAsync(persistence, causality);
        await RecordRouteDecisionAsync(persistence, causality, shouldCloseEpic);

        Assert.True(await SettleCompletionRouteAsync(repository, causality));

        CanonicalWorkflowStateRecord workflow = Assert.Single(
            (await persistence.LoadSnapshotAsync()).WorkflowStates);
        Assert.Equal(expectedStage, workflow.CurrentStage?.Value);
        Assert.Equal(WorkflowResolutionState.Resumable, workflow.State);
    }

    /// <summary>
    /// Fail-closed: with no typed decision recorded, settlement refuses to route rather than
    /// guessing a successor, and says which fact is missing. A rendered output that plainly states
    /// the epic should close is present precisely so that a parsing fallback would be visible here
    /// as a pass.
    /// </summary>
    [Fact]
    public async Task Completion_routing_fails_closed_when_the_typed_decision_fact_is_absent()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCompletionRouteRunAsync(persistence);
        await RecordRenderedOutputAsync(
            persistence, causality, "The epic is complete and should close. Should Close Epic: true.");

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SettleCompletionRouteAsync(repository, causality));

        // Names the fact that is missing, and the run it is missing from, so the stop is actionable.
        // Asserts the readable quoted form specifically - a message that instead embedded the
        // TransitionRunIdentity struct's own ToString (e.g. "TransitionRunIdentity { Value = ... }")
        // would still satisfy a bare substring check on the raw value, so that is not enough here.
        Assert.Contains(CompletionRouteDecision.EventName, failure.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"Transition run '{causality.TransitionRun.Value}' carries no durable",
            failure.Message,
            StringComparison.Ordinal);
        // The run stays unsettled: a refused route must not half-advance the workflow.
        Assert.Equal(
            TransitionDurableState.EffectsPending,
            Assert.Single((await persistence.LoadSnapshotAsync()).TransitionRuns).State);
    }

    /// <summary>
    /// Fail-closed also on a malformed fact: a `CompletionRouteDecided` row whose document JSON is
    /// a well-formed object that simply lacks `shouldCloseEpic` - a rename, or any other producer -
    /// must hit the same fail-stop as no row at all, never silently default to "continue". This
    /// guards <c>FromDocumentJson</c> against System.Text.Json filling an unmatched constructor
    /// parameter with <c>default(bool)</c> instead of failing.
    /// </summary>
    [Fact]
    public async Task Completion_routing_fails_closed_when_the_decision_fact_is_malformed()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCompletionRouteRunAsync(persistence);
        await RecordRenderedOutputAsync(
            persistence, causality, "The epic is complete and should close. Should Close Epic: true.");
        await RecordMalformedRouteDecisionAsync(persistence, causality);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SettleCompletionRouteAsync(repository, causality));

        Assert.Contains(CompletionRouteDecision.EventName, failure.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"Transition run '{causality.TransitionRun.Value}' carries no durable",
            failure.Message,
            StringComparison.Ordinal);
        // The run stays unsettled: a refused route must not half-advance the workflow.
        Assert.Equal(
            TransitionDurableState.EffectsPending,
            Assert.Single((await persistence.LoadSnapshotAsync()).TransitionRuns).State);
    }

    /// <summary>Puts the run at the last transition of Execute's Completion stage, which is the
    /// only position from which stage routing consults the completion route.</summary>
    private static async Task<CanonicalCausalContext> SeedCompletionRouteRunAsync(
        CanonicalWorkflowPersistenceStore persistence)
    {
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        TransitionRuntimeRequest request = CompletionRouteRequest(causality);
        WorkflowTransitionDefinition definition = CompletionRouteDefinition();
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        await new CanonicalTransitionRunStore(persistence).PersistStartedAsync(
            new TransitionRunStarted(
                causality,
                DateTimeOffset.UtcNow,
                request,
                definition,
                new TransitionInputSnapshot("snapshot", [], new Dictionary<string, string>(), []),
                prompt),
            CancellationToken.None);
        await new CanonicalTransitionCommitStore(persistence).CommitAsync(
            new TransitionCommitCapture(
                causality,
                request,
                definition,
                new ProductValidationResult(
                    ProductValidationStatus.Valid, [CompletionRouteProduct(causality)], [], [], [], [],
                    "valid", ["validator"]),
                new GateResult(GateStatus.Satisfied, [], "satisfied", ["gate"]),
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        return causality;
    }

    private static Task RecordRenderedOutputAsync(
        CanonicalWorkflowPersistenceStore persistence,
        CanonicalCausalContext causality,
        string rawOutput = "# Completion Route\n\nCertification finished. See the route fact.") =>
        new CanonicalTransitionEvidenceStore(persistence).RecordRawOutputAsync(
            causality,
            new WorkflowTransitionIdentity("InterpretCompletionRoute"),
            new PromptExecutionResult(
                PromptExecutionStatus.Completed, rawOutput, TimeSpan.FromSeconds(1),
                new Dictionary<string, string>()),
            CancellationToken.None);

    private static Task RecordRouteDecisionAsync(
        CanonicalWorkflowPersistenceStore persistence,
        CanonicalCausalContext causality,
        bool shouldCloseEpic) =>
        persistence.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                causality.TransitionRun.Value,
                new WorkflowTransitionIdentity("InterpretCompletionRoute"),
                CompletionRouteDecision.EventName,
                DateTimeOffset.UtcNow,
                TransitionDurableState.PromptCompleted,
                "Completion certification decided the epic route.",
                ["completion-route-decision"],
                new CompletionRouteDecision(shouldCloseEpic).ToDocumentJson()));

    /// <summary>
    /// Records a `CompletionRouteDecided` row whose document JSON is a well-formed object lacking
    /// `shouldCloseEpic`, standing in for a rename or any other producer that drifts from the shape
    /// <see cref="CompletionRouteDecision"/> expects.
    /// </summary>
    private static Task RecordMalformedRouteDecisionAsync(
        CanonicalWorkflowPersistenceStore persistence,
        CanonicalCausalContext causality) =>
        persistence.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                causality.TransitionRun.Value,
                new WorkflowTransitionIdentity("InterpretCompletionRoute"),
                CompletionRouteDecision.EventName,
                DateTimeOffset.UtcNow,
                TransitionDurableState.PromptCompleted,
                "Completion certification recorded a decision row missing its shouldCloseEpic field.",
                ["malformed-route-decision"],
                "{}"));

    /// <summary>Settles the run's one effect so routing runs, against the real catalog.</summary>
    private static async Task<bool> SettleCompletionRouteAsync(
        Repository repository,
        CanonicalCausalContext causality)
    {
        var effects = new CanonicalEffectWorkStore(repository);
        EffectScanRow work = Assert.Single(
            await effects.ScanUnsettledAsync(10, DateTimeOffset.UtcNow, CancellationToken.None));
        await effects.RecordReceiptAsync(
            work.Intent.Identity,
            new EffectReceipt(
                EffectReceiptIdentity.New(), work.Intent.Identity, work.Intent.Executor,
                work.Intent.ExecutorVersion, work.Intent.Target.Identity, "before", "after",
                true, "test:effect", ["independent-observation"], DateTimeOffset.UtcNow),
            "test-worker",
            CancellationToken.None);
        return await new CanonicalEffectPlanSettlementStore(repository)
            .TrySettleAsync(causality.TransitionRun, CancellationToken.None);
    }

    private static TransitionRuntimeRequest CompletionRouteRequest(CanonicalCausalContext causality)
    {
        var execution = new CanonicalTransitionExecutionContext(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            causality.Workspace,
            causality.Run,
            causality.WorkflowInstance,
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test"));
        return new TransitionRuntimeRequest(
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Completion"),
            new WorkflowTransitionIdentity("InterpretCompletionRoute"),
            execution,
            FreshAttemptAuthorization.Instance);
    }

    private static WorkflowTransitionDefinition CompletionRouteDefinition() => new(
        new WorkflowTransitionIdentity("InterpretCompletionRoute"),
        "interpret completion route",
        [],
        new GateDefinition(new GateIdentity("input"), "input", [], "test", "fail"),
        "InterpretCompletionRoute",
        ExecutionPosture.OneShotAgentPrompt,
        [],
        new GateDefinition(new GateIdentity("output"), "output", [], "test", "fail"),
        [],
        [new EffectDefinition(new EffectIdentity("persist"), EffectCategory.ProductPersistence,
            "validated", [], [], 1, "retry")],
        [], [],
        new RecoveryDefinition("recovery", "recover", ["retry"], []));

    private static ProductRecord CompletionRouteProduct(CanonicalCausalContext causality) => new(
        ProductIdentity.CompletionRoute,
        WorkflowIdentity.Execute,
        new WorkflowTransitionIdentity("InterpretCompletionRoute"),
        [WorkflowIdentity.Execute],
        "repository",
        "test",
        [".agents/completion-route.json"],
        causality.Attempt.Value,
        ProductFreshness.Fresh,
        ProductValidationState.Valid,
        ProductLifecycle.Active,
        [".agents/completion-route.json"]);

    [Fact]
    public async Task Recovery_coordinator_persists_canonical_cancelled_retry_plan_without_executing_work()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        TransitionRuntimeRequest request = Request(causality);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        var runs = new CanonicalTransitionRunStore(persistence);
        await runs.PersistStartedAsync(new TransitionRunStarted(
            causality,
            DateTimeOffset.UtcNow,
            request,
            definition,
            new TransitionInputSnapshot("snapshot", [], new Dictionary<string, string>(), []),
            prompt), CancellationToken.None);
        await new CanonicalTransitionBoundaryJournal(persistence).RecordAsync(
            new TransitionBoundaryObservation(
                causality,
                definition.Identity,
                TransitionBoundaryKind.PreSubmission,
                1,
                DateTimeOffset.UtcNow,
                "snapshot",
                null,
                []),
            CancellationToken.None);
        await runs.PersistStateAsync(
            new TransitionRunStateUpdate(
                causality, DateTimeOffset.UtcNow, definition.Identity,
                TransitionDurableState.Cancelled, "cancelled before submission", ["pre-submission"]),
            CancellationToken.None);
        var coordinator = new TransitionRecoveryCoordinator(
            runs,
            new CanonicalTransitionRecoveryPlanStore(persistence));

        TransitionRecoveryPlan plan = await coordinator.PlanAsync(causality.TransitionRun);

        Assert.Equal(CanonicalRecoveryAction.RetryNewAttempt, plan.Action);
        Assert.Equal(RecoveryAttemptMode.RetryExistingTransitionRun, plan.ResultingAttemptMode);
        Assert.Equal(causality.Attempt, plan.SourceCausality.Attempt);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT action FROM canonical_recovery_plans WHERE plan_id = $id;";
        command.Parameters.AddWithValue("$id", plan.RecoveryIdentity.Value);
        Assert.Equal("RetryNewAttempt", Convert.ToString(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task AppendAsync_ledger_sequence_is_1_based_insertion_order_including_pre_existing_rows()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);

        // Pre-existing, unrelated history the sequence must still count.
        const int historySize = 4;
        for (int i = 0; i < historySize; i++)
        {
            CanonicalCausalContext other = await SeedCausalityAsync(persistence);
            await promptStore.AppendAsync(PromptFact(other), CancellationToken.None);
        }

        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        PersistedRenderedPromptFact first = await promptStore.AppendAsync(PromptFact(causality), CancellationToken.None);
        PersistedRenderedPromptFact second = await promptStore.AppendAsync(PromptFact(causality), CancellationToken.None);
        PersistedRenderedPromptFact third = await promptStore.AppendAsync(PromptFact(causality), CancellationToken.None);

        // Same values the old FindIndex(...) + 1 over the full table would have produced.
        Assert.Equal(historySize + 1, first.LedgerSequence);
        Assert.Equal(historySize + 2, second.LedgerSequence);
        Assert.Equal(historySize + 3, third.LedgerSequence);
    }

    /// <summary>
    /// Characterization test for Task 3.2: <see cref="CanonicalRenderedPromptFactStore.ReadAsync"/>
    /// used to call <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptsAsync"/> - every
    /// rendered-prompt row in the workspace, in insertion order, including every other prompt's full
    /// <c>RenderedText</c> - and find the wanted one with <c>FindIndex</c>, computing the ledger
    /// position from that same index. It now reads the row by key and the ledger position by a
    /// separate counted query, neither of which loads any other row's <c>RenderedText</c>.
    /// <para>
    /// The baseline is <see cref="LegacyReadAsync"/>, a byte-for-byte copy of the pre-Task-3.2
    /// method body (verified identical via <c>git diff</c> against the pre-change file), executed
    /// for real against the same seeded workspace through the store methods this task left untouched
    /// (<c>ReadRenderedPromptsAsync</c>, <c>ReadAttemptsAsync</c>, <c>ReadWorkspaceIdentityAsync</c>).
    /// Comparing its real output to the keyed implementation's real output - rather than reasoning
    /// about the diff - is what makes this a characterization test.
    /// </para>
    /// <para>
    /// Two prompts (A and B) are seeded with distinct, independently identifiable
    /// <c>RenderedText</c> - A's deliberately large, to dramatize the row-size risk - after a run of
    /// unrelated pre-existing history whose own rendered text the keyed reads below must never load.
    /// This specifically catches the risk keyed SQL introduces that <c>FindIndex</c> over an
    /// in-memory list could not have: a <c>WHERE rendered_prompt_id = $id</c> clause that is wrong,
    /// or missing, silently returns the wrong row (or every row) instead of raising.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ReadAsync_matches_the_pre_keyed_implementation_and_does_not_cross_contaminate_prompts()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);

        // Pre-existing, unrelated history - large content of its own - the keyed reads for A and B
        // below must never load.
        const int historySize = 5;
        for (int i = 0; i < historySize; i++)
        {
            CanonicalCausalContext other = await SeedCausalityAsync(persistence);
            await promptStore.AppendAsync(
                PromptFact(other, new string('h', 10_000) + $"-history-{i}"), CancellationToken.None);
        }

        CanonicalCausalContext causalityA = await SeedCausalityAsync(persistence);
        RenderedPromptFact factA = PromptFact(causalityA, new string('a', 100_000) + "-prompt-a-marker");
        await promptStore.AppendAsync(factA, CancellationToken.None);

        CanonicalCausalContext causalityB = await SeedCausalityAsync(persistence);
        RenderedPromptFact factB = PromptFact(causalityB, "prompt-b-marker");
        await promptStore.AppendAsync(factB, CancellationToken.None);

        // Baseline, captured by executing the pre-Task-3.2 algorithm for real before trusting the
        // keyed replacement - not by reasoning about what it ought to return.
        PersistedRenderedPromptFact? expectedA =
            await LegacyReadAsync(persistence, factA.Identity, CancellationToken.None);
        PersistedRenderedPromptFact? expectedB =
            await LegacyReadAsync(persistence, factB.Identity, CancellationToken.None);
        Assert.NotNull(expectedA);
        Assert.NotNull(expectedB);

        // A fresh store instance: AppendAsync above populated the in-memory `appended` cache on
        // `promptStore`, which would short-circuit ReadAsync before it ever touches the database.
        var freshPromptStore = new CanonicalRenderedPromptFactStore(persistence);

        // Fix pass 1, finding 2: the invariant this test polices is "the unkeyed, full-hydration
        // ReadRenderedPromptsAsync path did not run" - not an exact statement-count total. The old
        // `Assert.Equal(6, counter.Statements)` here could not tell a reintroduced full-hydration
        // regression apart from a future, unrelated change to the attempt lookup's own statement
        // count (e.g. keying it): both move the total away from 6. A direct signal expresses the
        // actual invariant regardless of what the attempt lookup does. See
        // ReadRenderedPromptAsync_and_ReadRenderedPromptLedgerPositionAsync_together_compile_exactly_two_statements
        // below for a numeric bound scoped to just the prompt reads.
        bool hydrationPathInvoked = false;
        persistence.ReadRenderedPromptsAsyncInvokedForTesting = () => hydrationPathInvoked = true;
        PersistedRenderedPromptFact? actualB;
        try
        {
            actualB = await freshPromptStore.ReadAsync(factB.Identity, CancellationToken.None);
        }
        finally
        {
            persistence.ReadRenderedPromptsAsyncInvokedForTesting = null;
        }
        PersistedRenderedPromptFact? actualA =
            await freshPromptStore.ReadAsync(factA.Identity, CancellationToken.None);

        Assert.NotNull(actualA);
        Assert.NotNull(actualB);
        AssertSamePersistedFact(expectedA!, actualA!);
        AssertSamePersistedFact(expectedB!, actualB!);

        // The keyed path must never fall back to the full-table hydration it replaced - the actual
        // regression Task 3.2 exists to prevent, expressed directly instead of inferred from an
        // arithmetic total.
        Assert.False(
            hydrationPathInvoked,
            "ReadAsync must not fall back to the unkeyed, full-hydration ReadRenderedPromptsAsync.");

        // Discriminating assertions: AssertSamePersistedFact above only proves prompt B's keyed
        // result matches prompt B's *own* legacy result, which a bug that fed both calls the same
        // wrong row would not catch. These fail if prompt A's content or position leaked into B's
        // result, or vice versa - which is the entire point of seeding two distinct prompts.
        Assert.Equal(factB.RenderedContent, actualB!.Fact.RenderedContent);
        Assert.Equal(factA.RenderedContent, actualA!.Fact.RenderedContent);
        Assert.NotEqual(actualA.Fact.RenderedContent, actualB.Fact.RenderedContent);
        Assert.Equal(historySize + 1, actualA.LedgerSequence);
        Assert.Equal(historySize + 2, actualB.LedgerSequence);
        Assert.Equal(causalityA.Attempt, actualA.Fact.Causality.Attempt);
        Assert.Equal(causalityB.Attempt, actualB.Fact.Causality.Attempt);
        Assert.Equal(causalityA.Run, actualA.Fact.Causality.Run);
        Assert.Equal(causalityB.Run, actualB.Fact.Causality.Run);
        Assert.Equal(causalityA.WorkflowInstance, actualA.Fact.Causality.WorkflowInstance);
        Assert.Equal(causalityB.WorkflowInstance, actualB.Fact.Causality.WorkflowInstance);
    }

    /// <summary>
    /// Non-negotiable per Task 3.2: a keyed read must still return null for an id that does not
    /// exist in <c>canonical_rendered_prompts</c>, matching <see cref="LegacyReadAsync"/>'s behavior
    /// for the same case, rather than silently succeeding with a wrong or empty result.
    /// </summary>
    [Fact]
    public async Task ReadAsync_returns_null_for_a_rendered_prompt_id_that_does_not_exist()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        await promptStore.AppendAsync(PromptFact(causality), CancellationToken.None);

        var missing = new RenderedPromptFactIdentity("does-not-exist");
        var freshPromptStore = new CanonicalRenderedPromptFactStore(persistence);

        Assert.Null(await LegacyReadAsync(persistence, missing, CancellationToken.None));
        Assert.Null(await freshPromptStore.ReadAsync(missing, CancellationToken.None));
    }

    /// <summary>
    /// Ledger-position edge cases per Task 3.2: the very first row ever inserted into a fresh
    /// table (position 1 - an off-by-one here, e.g. <c>rowid &lt;</c> instead of <c>rowid &lt;=</c>,
    /// would read 0) and the most recently inserted row (position equal to the table's current row
    /// count - an off-by-one the other way, e.g. omitting the row's own match, would read one less
    /// than the true count). A third edge case the brief names - a prompt whose neighbours were
    /// deleted - does not apply: <c>canonical_rendered_prompts</c> has no production
    /// <c>DELETE</c> anywhere in this codebase (verified via
    /// <c>grep -rn "DELETE FROM canonical_rendered_prompts" src/</c>, zero matches), so no row's
    /// neighbours can ever be removed after insertion; the position query's own doc comment records
    /// this as the reason the counted approach is equivalent to the old in-memory index.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ledger_position_is_correct_for_the_first_and_last_prompt_in_the_table()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);

        CanonicalCausalContext causalityFirst = await SeedCausalityAsync(persistence);
        RenderedPromptFact factFirst = PromptFact(causalityFirst, "first-in-a-fresh-table");
        await promptStore.AppendAsync(factFirst, CancellationToken.None);

        for (int i = 0; i < 3; i++)
        {
            CanonicalCausalContext middle = await SeedCausalityAsync(persistence);
            await promptStore.AppendAsync(PromptFact(middle, $"middle-{i}"), CancellationToken.None);
        }

        CanonicalCausalContext causalityLast = await SeedCausalityAsync(persistence);
        RenderedPromptFact factLast = PromptFact(causalityLast, "last-in-the-table-so-far");
        await promptStore.AppendAsync(factLast, CancellationToken.None);

        // Fresh store: the two AppendAsync calls above populated `appended` for factFirst/factLast
        // on `promptStore`, which would short-circuit ReadAsync before it touches the database.
        var freshPromptStore = new CanonicalRenderedPromptFactStore(persistence);
        PersistedRenderedPromptFact? readFirst =
            await freshPromptStore.ReadAsync(factFirst.Identity, CancellationToken.None);
        PersistedRenderedPromptFact? readLast =
            await freshPromptStore.ReadAsync(factLast.Identity, CancellationToken.None);

        Assert.NotNull(readFirst);
        Assert.NotNull(readLast);
        Assert.Equal(1, readFirst!.LedgerSequence);
        Assert.Equal(5, readLast!.LedgerSequence);
    }

    /// <summary>
    /// Static proof, not a timing measurement, that the keyed rendered-prompt read is a primary-key
    /// seek rather than a table scan: a <c>SCAN canonical_rendered_prompts</c> plan would visit every
    /// row - including every other prompt's <c>rendered_text</c> - to find the one matching row. A
    /// statement-count assertion alone cannot show this: both the old full-table SELECT and this
    /// keyed SELECT compile as exactly one statement each, so the difference is in what each
    /// statement's execution touches, which only the query plan (or a timing measurement, which this
    /// codebase avoids - see <see cref="ReadAsync_matches_the_pre_keyed_implementation_and_does_not_cross_contaminate_prompts"/>'s
    /// statement-count assertion for the part that *is* provable that way) can show.
    /// </summary>
    [Fact]
    public async Task ReadRenderedPromptAsync_is_backed_by_the_primary_key_not_a_table_scan()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        RenderedPromptFact fact = PromptFact(causality);
        await promptStore.AppendAsync(fact, CancellationToken.None);

        IReadOnlyList<string> plan = await ExplainRenderedPromptLookupAsync(
            repository, CanonicalWorkflowPersistenceStore.ReadRenderedPromptSql, fact.Identity.Value);

        Assert.Contains(
            plan, step => step.Contains("SEARCH canonical_rendered_prompts", StringComparison.Ordinal));
        Assert.DoesNotContain(
            plan, step => step.Contains("SCAN canonical_rendered_prompts", StringComparison.Ordinal));
    }

    /// <summary>
    /// Fix pass 1, finding 3: after adding <c>rowid</c> to
    /// <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptSql"/> and threading it
    /// straight into <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptLedgerPositionAsync"/>,
    /// the record read and the position read compile as exactly one statement each. This is a bound
    /// scoped to just the prompt reads themselves - deliberately excluding the unrelated attempt
    /// lookup that <c>CanonicalRenderedPromptFactStore.ReadAsync</c> also depends on (see finding 2's
    /// complaint about the old, unscoped total) - by exercising the store's keyed methods directly
    /// rather than going through <c>ReadAsync</c>. A future change to the attempt lookup cannot move
    /// this number.
    /// </summary>
    [Fact]
    public async Task ReadRenderedPromptAsync_and_ReadRenderedPromptLedgerPositionAsync_together_compile_exactly_two_statements()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        RenderedPromptFact fact = PromptFact(causality);
        await promptStore.AppendAsync(fact, CancellationToken.None);

        var counter = new PreparedStatementCounter();
        persistence.ConnectionObserverForTesting = counter.Watch;
        (CanonicalRenderedPromptRecord? Record, long RowId) actual;
        try
        {
            actual = await persistence.ReadRenderedPromptAsync(fact.Identity.Value, CancellationToken.None);
            Assert.NotNull(actual.Record);
            _ = await persistence.ReadRenderedPromptLedgerPositionAsync(actual.RowId, CancellationToken.None);
        }
        finally
        {
            persistence.ConnectionObserverForTesting = null;
        }

        Assert.Equal(2, counter.Statements);
    }

    /// <summary>
    /// Fix pass 1, finding 1: <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptAsync"/>
    /// and <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptLedgerPositionAsync"/> used
    /// to issue their SQL directly, bypassing the same "no such table" tolerance every other spine
    /// read gets from the store's internal <c>ReadSpineRowsOrEmptyAsync</c>/<c>ReadSpineRowOrEmptyAsync</c>
    /// wrappers. On a pre-v3 workspace database - no <c>canonical_rendered_prompts</c> table at all -
    /// the old, full-hydration <c>CanonicalRenderedPromptFactStore.ReadAsync</c> returned
    /// <see langword="null"/>, while the keyed replacement threw a raw <see cref="SqliteException"/>.
    /// This pins the fail-closed behavior: <c>ReadAsync</c> must still return <see langword="null"/>,
    /// not throw, against such a database, and the ledger-position read must still report <c>0</c>.
    /// </summary>
    [Fact]
    public async Task ReadAsync_returns_null_rather_than_throwing_when_the_workspace_database_lacks_spine_tables()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            // A valid, initialized SQLite file with zero tables - standing in for a pre-v3 workspace
            // database, which lacks canonical_rendered_prompts (and every other spine table).
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 1;";
            await command.ExecuteNonQueryAsync();
        }

        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);

        PersistedRenderedPromptFact? result = await promptStore.ReadAsync(
            new RenderedPromptFactIdentity("does-not-matter"), CancellationToken.None);

        Assert.Null(result);

        // Directly pins the other new read too: a rowid looked up against a table-less database must
        // also read back as "no position" (0), not throw.
        long position = await persistence.ReadRenderedPromptLedgerPositionAsync(1, CancellationToken.None);
        Assert.Equal(0, position);
    }

    private static async Task<IReadOnlyList<string>> ExplainRenderedPromptLookupAsync(
        Repository repository, string sql, string renderedPromptId)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        command.Parameters.AddWithValue("$rendered_prompt_id", renderedPromptId);
        List<string> steps = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            steps.Add(reader.GetString(reader.GetOrdinal("detail")));
        }

        return steps;
    }

    /// <summary>
    /// Byte-for-byte reproduction of the pre-Task-3.2 <c>CanonicalRenderedPromptFactStore.ReadAsync</c>
    /// body (full rendered-prompt hydration + <c>FindIndex</c>), kept as the characterization
    /// baseline. It calls only store methods Task 3.2 left untouched, so it is the same algorithm
    /// that used to live in production before the keyed reads replaced it. Unlike the production
    /// method, it never consults the in-memory `appended` cache, since the characterization always
    /// needs to re-derive the result from the database.
    /// </summary>
    private static async Task<PersistedRenderedPromptFact?> LegacyReadAsync(
        CanonicalWorkflowPersistenceStore store,
        RenderedPromptFactIdentity prompt,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CanonicalRenderedPromptRecord> prompts =
            await store.ReadRenderedPromptsAsync(cancellationToken);
        int index = prompts.ToList().FindIndex(item => item.RenderedPromptId == prompt.Value);
        if (index < 0)
        {
            return null;
        }

        CanonicalRenderedPromptRecord record = prompts[index];
        if (record.AttemptId is null || record.PolicyId is null || record.PersistenceId is null ||
            record.PromptPolicyProfileId is null || record.ConsumedInputManifestId is null)
        {
            return null;
        }

        IReadOnlyList<AttemptRecord> attempts = await store.ReadAttemptsAsync(cancellationToken);
        AttemptRecord? attempt = attempts.SingleOrDefault(item => item.AttemptId == record.AttemptId);
        if (attempt is null)
        {
            return null;
        }

        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(await store.ReadWorkspaceIdentityAsync(cancellationToken)),
            new RunIdentity(attempt.RunId),
            new WorkflowInstanceIdentity(attempt.WorkflowInstanceId),
            new TransitionRunIdentity(record.TransitionRunId),
            new AttemptIdentity(record.AttemptId));
        var fact = new RenderedPromptFact(
            new RenderedPromptFactIdentity(record.RenderedPromptId),
            causality,
            record.RenderedText,
            record.RenderedSha256,
            new PromptTemplateIdentity(record.PromptIdentity),
            record.TemplateSourceHash,
            new PolicyIdentity(record.PolicyId),
            new PromptPolicyProfileIdentity(record.PromptPolicyProfileId),
            new ConsumedInputManifestIdentity(record.ConsumedInputManifestId),
            record.ConsumedInputs.Select(input => new ConsumedInputFile(input.Path, input.Sha256)).ToArray(),
            record.RenderedAt,
            record.RenderedEncoding);
        return new PersistedRenderedPromptFact(
            fact,
            new RenderedPromptPersistenceIdentity(record.PersistenceId),
            index + 1,
            record.RenderedAt);
    }

    private static void AssertSamePersistedFact(
        PersistedRenderedPromptFact expected, PersistedRenderedPromptFact actual)
    {
        Assert.Equal(expected.PersistenceIdentity, actual.PersistenceIdentity);
        Assert.Equal(expected.LedgerSequence, actual.LedgerSequence);
        Assert.Equal(expected.PersistedAt, actual.PersistedAt);

        Assert.Equal(expected.Fact.Identity, actual.Fact.Identity);
        Assert.Equal(expected.Fact.Causality, actual.Fact.Causality);
        Assert.Equal(expected.Fact.RenderedContent, actual.Fact.RenderedContent);
        Assert.Equal(expected.Fact.ContentHash, actual.Fact.ContentHash);
        Assert.Equal(expected.Fact.TemplateIdentity, actual.Fact.TemplateIdentity);
        Assert.Equal(expected.Fact.TemplateSourceHash, actual.Fact.TemplateSourceHash);
        Assert.Equal(expected.Fact.PolicyIdentity, actual.Fact.PolicyIdentity);
        Assert.Equal(expected.Fact.PolicyProfileIdentity, actual.Fact.PolicyProfileIdentity);
        Assert.Equal(expected.Fact.ConsumedInputManifestIdentity, actual.Fact.ConsumedInputManifestIdentity);
        Assert.Equal(expected.Fact.RenderedAt, actual.Fact.RenderedAt);
        Assert.Equal(expected.Fact.RenderedEncoding, actual.Fact.RenderedEncoding);
        Assert.Equal(expected.Fact.ConsumedInputs, actual.Fact.ConsumedInputs);
    }

    [Fact]
    public async Task ReadTransitionRunAsync_matches_full_snapshot_lookup_for_existing_and_missing_runs()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);
        var seededRunIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
            PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
                .AppendAsync(PromptFact(causality), CancellationToken.None);
            await runs.PersistStartedAsync(new TransitionRunStarted(
                causality,
                DateTimeOffset.UtcNow,
                Request(causality),
                definition,
                new TransitionInputSnapshot($"snapshot-{i}", [], new Dictionary<string, string>(), []),
                prompt), CancellationToken.None);
            seededRunIds.Add(causality.TransitionRun.Value);
        }

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(3, snapshot.TransitionRuns.Count);

        foreach (string runId in seededRunIds)
        {
            CanonicalTransitionRunRecord expected = Assert.Single(
                snapshot.TransitionRuns, run => run.RunId == runId);
            CanonicalTransitionRunRecord? actual = await persistence.ReadTransitionRunAsync(runId, CancellationToken.None);
            Assert.NotNull(actual);
            AssertSameTransitionRun(expected, actual!);
        }

        Assert.Null(await persistence.ReadTransitionRunAsync("does-not-exist", CancellationToken.None));
    }

    [Fact]
    public async Task PersistStateAsync_updates_the_correct_existing_run_when_history_has_many_rows()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);

        // Unrelated history rows the keyed read must not be distracted by.
        for (int i = 0; i < 5; i++)
        {
            CanonicalCausalContext other = await SeedCausalityAsync(persistence);
            PersistedRenderedPromptFact otherPrompt = await new CanonicalRenderedPromptFactStore(persistence)
                .AppendAsync(PromptFact(other), CancellationToken.None);
            await runs.PersistStartedAsync(new TransitionRunStarted(
                other,
                DateTimeOffset.UtcNow,
                Request(other),
                definition,
                new TransitionInputSnapshot($"other-{i}", [], new Dictionary<string, string>(), []),
                otherPrompt), CancellationToken.None);
        }

        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        await runs.PersistStartedAsync(new TransitionRunStarted(
            causality,
            DateTimeOffset.UtcNow,
            Request(causality),
            definition,
            new TransitionInputSnapshot("target", [], new Dictionary<string, string>(), []),
            prompt), CancellationToken.None);

        CanonicalTransitionRunRecord before = Assert.Single(
            (await persistence.LoadSnapshotAsync()).TransitionRuns,
            run => run.RunId == causality.TransitionRun.Value);

        var update = new TransitionRunStateUpdate(
            causality, DateTimeOffset.UtcNow, definition.Identity,
            TransitionDurableState.Stalled, "stalled explanation", ["stalled-evidence"]);
        await runs.PersistStateAsync(update, CancellationToken.None);

        CanonicalWorkflowPersistenceSnapshot afterSnapshot = await persistence.LoadSnapshotAsync();
        CanonicalTransitionRunRecord after = Assert.Single(
            afterSnapshot.TransitionRuns, run => run.RunId == causality.TransitionRun.Value);

        // Fields untouched by PersistStateAsync must survive from the pre-update row, proving the
        // keyed read found the real seeded row rather than falling back.
        Assert.Equal(before.RunId, after.RunId);
        Assert.Equal(before.Workflow, after.Workflow);
        Assert.Equal(before.Stage, after.Stage);
        Assert.Equal(before.Transition, after.Transition);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.InputSnapshotHash, after.InputSnapshotHash);

        // Fields PersistStateAsync overwrites must reflect the update.
        Assert.Equal(TransitionDurableState.Stalled, after.State);
        Assert.Equal(RuntimeOutcomeKind.Stalled, after.Outcome);
        Assert.Equal(update.RecordedAt, after.CompletedAt);
        Assert.Equal("stalled explanation", after.Explanation);
        Assert.Equal(new[] { "stalled-evidence" }, after.Evidence);

        // The unrelated history rows were left alone.
        Assert.Equal(6, afterSnapshot.TransitionRuns.Count);
    }

    [Fact]
    public async Task PersistStateAsync_builds_the_documented_fallback_record_when_no_start_record_exists()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);

        // An unrelated run must not be mistaken for the orphaned one below.
        CanonicalCausalContext other = await SeedCausalityAsync(persistence);
        PersistedRenderedPromptFact otherPrompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(other), CancellationToken.None);
        await runs.PersistStartedAsync(new TransitionRunStarted(
            other,
            DateTimeOffset.UtcNow,
            Request(other),
            definition,
            new TransitionInputSnapshot("other", [], new Dictionary<string, string>(), []),
            otherPrompt), CancellationToken.None);

        CanonicalCausalContext orphan = await SeedCausalityAsync(persistence);
        var transition = new WorkflowTransitionIdentity("WritePlan");
        var update = new TransitionRunStateUpdate(
            orphan, DateTimeOffset.UtcNow, transition,
            TransitionDurableState.Completed, "completed without a start record", ["orphan-evidence"]);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        await runs.PersistStateAsync(update, CancellationToken.None);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(2, snapshot.TransitionRuns.Count);
        CanonicalTransitionRunRecord fallback = Assert.Single(
            snapshot.TransitionRuns, run => run.RunId == orphan.TransitionRun.Value);

        Assert.Equal(new WorkflowIdentity("Unknown"), fallback.Workflow);
        Assert.Equal(new WorkflowStageIdentity("Unknown"), fallback.Stage);
        Assert.Equal(transition, fallback.Transition);
        Assert.Null(fallback.InputSnapshotHash);
        Assert.Equal(TransitionDurableState.Completed, fallback.State);
        Assert.Equal(RuntimeOutcomeKind.Completed, fallback.Outcome);
        Assert.Equal(update.RecordedAt, fallback.CompletedAt);
        Assert.Equal("completed without a start record", fallback.Explanation);
        Assert.Equal(new[] { "orphan-evidence" }, fallback.Evidence);
        Assert.InRange(fallback.StartedAt, before, after);
    }

    [Fact]
    public async Task ReadTransitionRunAsync_reads_exactly_one_row_no_matter_how_large_the_history_table_is()
    {
        const int historySize = 50;
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);

        for (int i = 0; i < historySize; i++)
        {
            CanonicalCausalContext other = await SeedCausalityAsync(persistence);
            PersistedRenderedPromptFact otherPrompt = await new CanonicalRenderedPromptFactStore(persistence)
                .AppendAsync(PromptFact(other), CancellationToken.None);
            await runs.PersistStartedAsync(new TransitionRunStarted(
                other,
                DateTimeOffset.UtcNow,
                Request(other),
                definition,
                new TransitionInputSnapshot($"history-{i}", [], new Dictionary<string, string>(), []),
                otherPrompt), CancellationToken.None);
        }

        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), CancellationToken.None);
        await runs.PersistStartedAsync(new TransitionRunStarted(
            causality,
            DateTimeOffset.UtcNow,
            Request(causality),
            definition,
            new TransitionInputSnapshot("target", [], new Dictionary<string, string>(), []),
            prompt), CancellationToken.None);

        // Ground truth: the full snapshot's transition-run table has grown to historySize + 1 rows.
        // The old ExistingOrFallbackAsync (LoadSnapshotAsync().TransitionRuns.FirstOrDefault) would
        // have read all of them, on every single PersistStateAsync call, regardless of which one it
        // actually needed.
        CanonicalWorkflowPersistenceSnapshot snapshot = await persistence.LoadSnapshotAsync();
        Assert.Equal(historySize + 1, snapshot.TransitionRuns.Count);

        // The keyed read the current ExistingOrFallbackAsync uses reads exactly one row -- flat,
        // not proportional to historySize.
        CanonicalTransitionRunRecord? keyed = await persistence.ReadTransitionRunAsync(
            causality.TransitionRun.Value, CancellationToken.None);
        Assert.NotNull(keyed);
        Assert.Equal(causality.TransitionRun.Value, keyed!.RunId);

        await runs.PersistStateAsync(
            new TransitionRunStateUpdate(
                causality, DateTimeOffset.UtcNow, definition.Identity,
                TransitionDurableState.Stalled, "measured", ["measured"]),
            CancellationToken.None);

        // The write via the keyed path did not touch or duplicate any unrelated history rows.
        Assert.Equal(historySize + 1, (await persistence.LoadSnapshotAsync()).TransitionRuns.Count);
    }

    /// <summary>
    /// Characterization test for Task 3.1: <c>CanonicalTransitionRunStore.LoadRecoveryAsync</c> used
    /// to call <see cref="CanonicalWorkflowPersistenceStore.LoadSnapshotAsync"/> (the full nine-table
    /// snapshot) plus three more full-table reads (attempts, workflow instances, runs), and filter
    /// everything down to one run in memory. It now issues one keyed read per table plus point
    /// lookups instead. Two runs are seeded with distinct evidence, effects, and attempts specifically
    /// to catch the risk keyed SQL introduces that in-memory filtering could not have: a
    /// <c>WHERE run_id = $run</c> clause that is wrong, or missing, silently drops or
    /// cross-contaminates rows rather than raising - a single-run fixture cannot discriminate that
    /// from a working implementation.
    /// <para>
    /// The baseline is <see cref="LegacyLoadRecoveryAsync"/>, a byte-for-byte copy of the
    /// pre-Task-3.1 method body (verified identical via <c>git diff</c> against the pre-change file),
    /// executed for real against the same seeded workspace through the store methods this task left
    /// untouched (<c>LoadSnapshotAsync</c>, <c>ReadAttemptsAsync</c>, <c>ReadWorkflowInstancesAsync</c>,
    /// <c>ReadRunsAsync</c>). Comparing its real output to the keyed implementation's real output -
    /// rather than reasoning about the diff - is what makes this a characterization test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task LoadRecoveryAsync_matches_the_pre_keyed_implementation_and_does_not_cross_contaminate_runs()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var runs = new CanonicalTransitionRunStore(persistence);

        TransitionRunIdentity transitionA =
            await SeedRecoveryFixtureAsync(persistence, runs, repository, "a", CancellationToken.None);
        TransitionRunIdentity transitionB =
            await SeedRecoveryFixtureAsync(persistence, runs, repository, "b", CancellationToken.None);

        // Baseline, captured by executing the pre-Task-3.1 algorithm for real before trusting the
        // keyed replacement - not by reasoning about what it ought to return.
        TransitionRunRecoverySnapshot? expectedA =
            await LegacyLoadRecoveryAsync(persistence, transitionA, CancellationToken.None);
        TransitionRunRecoverySnapshot? expectedB =
            await LegacyLoadRecoveryAsync(persistence, transitionB, CancellationToken.None);
        Assert.NotNull(expectedA);
        Assert.NotNull(expectedB);

        var counter = new PreparedStatementCounter();
        persistence.ConnectionObserverForTesting = counter.Watch;
        TransitionRunRecoverySnapshot? actualA;
        try
        {
            actualA = await runs.LoadRecoveryAsync(transitionA, CancellationToken.None);
        }
        finally
        {
            persistence.ConnectionObserverForTesting = null;
        }
        TransitionRunRecoverySnapshot? actualB = await runs.LoadRecoveryAsync(transitionB, CancellationToken.None);

        Assert.NotNull(actualA);
        Assert.NotNull(actualB);
        AssertSameRecoverySnapshot(expectedA!, actualA!);
        AssertSameRecoverySnapshot(expectedB!, actualB!);

        // Counted, not modelled: three `WHERE run_id = $run` reads (transition run, evidence, effect
        // records) plus three point lookups (latest attempt, workflow instance, root run) for the run
        // A call the counter was installed around.
        Assert.Equal(6, counter.Statements);

        // Discriminating assertions: AssertSameRecoverySnapshot above only proves run A's keyed result
        // matches run A's *own* legacy result, which a bug that fed both calls the same wrong rows
        // would not catch. These assertions fail if any of run B's rows or markers leaked into run
        // A's result, or vice versa - which is the entire point of seeding two runs.
        Assert.Equal("raw-a", actualA!.RawOutput!.RawOutput);
        Assert.Equal("raw-b", actualB!.RawOutput!.RawOutput);
        Assert.Equal(2, actualA.Boundaries.Count);
        Assert.Equal(2, actualB.Boundaries.Count);
        Assert.Equal(2, actualA.Effects.Count);
        Assert.Equal(2, actualB.Effects.Count);
        Assert.All(actualA.Boundaries, boundary => Assert.All(
            boundary.Evidence, item => Assert.StartsWith("boundary-a-", item, StringComparison.Ordinal)));
        Assert.All(actualB.Boundaries, boundary => Assert.All(
            boundary.Evidence, item => Assert.StartsWith("boundary-b-", item, StringComparison.Ordinal)));
        Assert.All(actualA.Effects, effect => Assert.All(
            effect.Evidence, item => Assert.StartsWith("effect-a-", item, StringComparison.Ordinal)));
        Assert.All(actualB.Effects, effect => Assert.All(
            effect.Evidence, item => Assert.StartsWith("effect-b-", item, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Byte-for-byte reproduction of the pre-Task-3.1 <c>CanonicalTransitionRunStore.LoadRecoveryAsync</c>
    /// body (full snapshot + in-memory LINQ filter), kept as the characterization baseline. It calls
    /// only store methods Task 3.1 left untouched, so it is the same algorithm that used to live in
    /// production before keyed reads replaced it.
    /// </summary>
    private static async Task<TransitionRunRecoverySnapshot?> LegacyLoadRecoveryAsync(
        CanonicalWorkflowPersistenceStore store,
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync(cancellationToken);
        string runId = transitionRun.Value;
        CanonicalTransitionRunRecord? run = snapshot.TransitionRuns.SingleOrDefault(item => item.RunId == runId);
        if (run is null)
        {
            return null;
        }

        PromptExecutionResult? rawOutput = snapshot.TransitionEvidence
            .Where(item => item.RunId == runId && item.EventName == "RawPromptOutputCaptured")
            .OrderByDescending(item => item.EvidenceId)
            .Select(item => LegacyDeserialize<PromptExecutionResult>(item.DocumentJson))
            .FirstOrDefault(item => item is not null);
        TransitionBoundaryObservation[] boundaries = snapshot.TransitionEvidence
            .Where(item => item.RunId == runId && item.EventName == "TransitionBoundaryObserved")
            .OrderBy(item => item.EvidenceId)
            .Select(item => LegacyDeserialize<TransitionBoundaryObservation>(item.DocumentJson))
            .Where(item => item is not null)
            .Cast<TransitionBoundaryObservation>()
            .ToArray();
        EffectExecutionRecord[] effects = snapshot.EffectRecords
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.RecordId)
            .Select(item => new EffectExecutionRecord(item.Effect, item.Status, item.Explanation, item.Evidence))
            .ToArray();
        IReadOnlyList<AttemptRecord> attempts = await store.ReadAttemptsAsync(cancellationToken);
        IReadOnlyList<WorkflowInstanceRecord> instances = await store.ReadWorkflowInstancesAsync(cancellationToken);
        IReadOnlyList<RunRecord> rootRuns = await store.ReadRunsAsync(cancellationToken);
        AttemptRecord? attempt = attempts
            .Where(item => item.TransitionRunId == runId)
            .OrderByDescending(item => item.AttemptIndex)
            .FirstOrDefault();
        WorkflowInstanceRecord? instance = attempt is null
            ? null
            : instances.SingleOrDefault(item => item.WorkflowInstanceId == attempt.WorkflowInstanceId);
        RunRecord? rootRun = attempt is null
            ? null
            : rootRuns.SingleOrDefault(item => item.RunId == attempt.RunId);
        if (attempt is null || instance is null || rootRun is null)
        {
            return null;
        }

        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(rootRun.WorkspaceId),
            new RunIdentity(rootRun.RunId),
            new WorkflowInstanceIdentity(instance.WorkflowInstanceId),
            transitionRun,
            new AttemptIdentity(attempt.AttemptId));
        return new TransitionRunRecoverySnapshot(
            causality,
            run.Transition,
            run.State,
            run.Outcome,
            run.InputSnapshotHash,
            rawOutput,
            effects,
            boundaries,
            run.Explanation,
            run.Evidence);
    }

    private static readonly JsonSerializerOptions LegacyRecoveryJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static T? LegacyDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, LegacyRecoveryJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// Seeds one recovery-eligible transition run - started, with a superseded and a winning raw
    /// output, two boundary observations, two effect records, and the attempt/instance/root-run chain
    /// - with every piece of content tagged by <paramref name="label"/> so a second call with a
    /// different label produces a fully independent, distinguishable run.
    /// </summary>
    private static async Task<TransitionRunIdentity> SeedRecoveryFixtureAsync(
        CanonicalWorkflowPersistenceStore persistence,
        CanonicalTransitionRunStore runs,
        Repository repository,
        string label,
        CancellationToken cancellationToken)
    {
        CanonicalCausalContext causality = await SeedCausalityAsync(persistence);
        WorkflowTransitionDefinition definition = Definition(withEffect: false);
        PersistedRenderedPromptFact prompt = await new CanonicalRenderedPromptFactStore(persistence)
            .AppendAsync(PromptFact(causality), cancellationToken);
        await runs.PersistStartedAsync(new TransitionRunStarted(
            causality,
            DateTimeOffset.UtcNow,
            Request(causality),
            definition,
            new TransitionInputSnapshot($"snapshot-{label}", [], new Dictionary<string, string>(), []),
            prompt), cancellationToken);

        var evidenceStore = new CanonicalTransitionEvidenceStore(persistence);
        // A superseded raw output: must lose to the later one below, proving the keyed read still
        // respects "latest wins" (OrderByDescending(EvidenceId)) rather than just "only wins".
        await evidenceStore.RecordRawOutputAsync(
            causality,
            definition.Identity,
            new PromptExecutionResult(
                PromptExecutionStatus.Completed, $"stale-{label}", TimeSpan.FromSeconds(1),
                new Dictionary<string, string>(StringComparer.Ordinal)),
            cancellationToken);
        await evidenceStore.RecordRawOutputAsync(
            causality,
            definition.Identity,
            new PromptExecutionResult(
                PromptExecutionStatus.Completed, $"raw-{label}", TimeSpan.FromSeconds(2),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["run"] = label }),
            cancellationToken);

        var journal = new CanonicalTransitionBoundaryJournal(persistence);
        await journal.RecordAsync(
            new TransitionBoundaryObservation(
                causality, definition.Identity, TransitionBoundaryKind.PreResolution, 0, DateTimeOffset.UtcNow,
                $"snapshot-{label}", null, [$"boundary-{label}-0"]),
            cancellationToken);
        await journal.RecordAsync(
            new TransitionBoundaryObservation(
                causality, definition.Identity, TransitionBoundaryKind.ProviderCompleted, 1, DateTimeOffset.UtcNow,
                $"snapshot-{label}", $"turn-{label}", [$"boundary-{label}-1"]),
            cancellationToken);

        // canonical_effect_records has no production writer left (Phase 2 moved effect tracking to
        // canonical_effect_intents), so the fixture inserts directly - the same way
        // LoopRelayWorkspaceDatabaseSchemaV9Tests seeds legacy effect-record rows - to exercise the
        // read path LoadRecoveryAsync still depends on for this table.
        await InsertEffectRecordAsync(
            repository, causality.TransitionRun.Value, $"effect-{label}-0", "Publication", "Succeeded",
            $"effect explanation {label} 0", [$"effect-{label}-0"], cancellationToken);
        await InsertEffectRecordAsync(
            repository, causality.TransitionRun.Value, $"effect-{label}-1", "ProductPersistence", "Failed",
            $"effect explanation {label} 1", [$"effect-{label}-1"], cancellationToken);

        return causality.TransitionRun;
    }

    private static async Task InsertEffectRecordAsync(
        Repository repository,
        string runId,
        string effectIdentity,
        string category,
        string status,
        string explanation,
        string[] evidence,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_effect_records (
                run_id, effect_identity, category, status, recorded_at, explanation, evidence_json
            ) VALUES (
                $run_id, $effect_identity, $category, $status, $recorded_at, $explanation, $evidence_json
            );
            """;
        command.Parameters.AddWithValue("$run_id", runId);
        command.Parameters.AddWithValue("$effect_identity", effectIdentity);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue(
            "$recorded_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$explanation", explanation);
        command.Parameters.AddWithValue("$evidence_json", JsonSerializer.Serialize(evidence));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AssertSameRecoverySnapshot(
        TransitionRunRecoverySnapshot expected, TransitionRunRecoverySnapshot actual)
    {
        Assert.Equal(expected.Causality, actual.Causality);
        Assert.Equal(expected.Transition, actual.Transition);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.InputSnapshotHash, actual.InputSnapshotHash);
        Assert.Equal(expected.Explanation, actual.Explanation);
        Assert.Equal(expected.Evidence, actual.Evidence);

        Assert.Equal(expected.RawOutput?.Status, actual.RawOutput?.Status);
        Assert.Equal(expected.RawOutput?.RawOutput, actual.RawOutput?.RawOutput);
        Assert.Equal(expected.RawOutput?.Duration, actual.RawOutput?.Duration);
        Assert.Equal(expected.RawOutput?.FailureMessage, actual.RawOutput?.FailureMessage);
        Assert.Equal(expected.RawOutput?.Metadata, actual.RawOutput?.Metadata);

        Assert.Equal(expected.Effects.Count, actual.Effects.Count);
        for (int index = 0; index < expected.Effects.Count; index++)
        {
            Assert.Equal(expected.Effects[index].Effect, actual.Effects[index].Effect);
            Assert.Equal(expected.Effects[index].Status, actual.Effects[index].Status);
            Assert.Equal(expected.Effects[index].Explanation, actual.Effects[index].Explanation);
            Assert.Equal(expected.Effects[index].Evidence, actual.Effects[index].Evidence);
        }

        Assert.Equal(expected.Boundaries.Count, actual.Boundaries.Count);
        for (int index = 0; index < expected.Boundaries.Count; index++)
        {
            Assert.Equal(expected.Boundaries[index].Causality, actual.Boundaries[index].Causality);
            Assert.Equal(expected.Boundaries[index].Transition, actual.Boundaries[index].Transition);
            Assert.Equal(expected.Boundaries[index].Boundary, actual.Boundaries[index].Boundary);
            Assert.Equal(expected.Boundaries[index].Sequence, actual.Boundaries[index].Sequence);
            Assert.Equal(expected.Boundaries[index].ObservedAt, actual.Boundaries[index].ObservedAt);
            Assert.Equal(expected.Boundaries[index].InputSnapshotHash, actual.Boundaries[index].InputSnapshotHash);
            Assert.Equal(expected.Boundaries[index].ProviderTurnId, actual.Boundaries[index].ProviderTurnId);
            Assert.Equal(expected.Boundaries[index].Evidence, actual.Boundaries[index].Evidence);
        }
    }

    private static void AssertSameTransitionRun(CanonicalTransitionRunRecord expected, CanonicalTransitionRunRecord actual)
    {
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.Workflow, actual.Workflow);
        Assert.Equal(expected.Stage, actual.Stage);
        Assert.Equal(expected.Transition, actual.Transition);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.StartedAt, actual.StartedAt);
        Assert.Equal(expected.CompletedAt, actual.CompletedAt);
        Assert.Equal(expected.InputSnapshotHash, actual.InputSnapshotHash);
        Assert.Equal(expected.Explanation, actual.Explanation);
        Assert.Equal(expected.Evidence, actual.Evidence);
    }

    private static async Task<CanonicalCausalContext> SeedCausalityAsync(
        CanonicalWorkflowPersistenceStore persistence)
    {
        WorkspaceIdentity workspace = new(await persistence.ReadWorkspaceIdentityAsync());
        RunIdentity run = RunIdentity.New();
        WorkflowInstanceIdentity instance = WorkflowInstanceIdentity.New();
        TransitionRunIdentity transition = TransitionRunIdentity.New();
        AttemptIdentity attempt = AttemptIdentity.New();
        await persistence.UpsertRunAsync(new RunRecord(
            run.Value,
            workspace.Value,
            "test-chain",
            InvocationModeKind.BoundedPlan.ToString(),
            "Active",
            DateTimeOffset.UtcNow,
            null,
            null,
            "test"));
        await persistence.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            instance.Value,
            run.Value,
            WorkflowIdentity.Plan,
            "test",
            "Active",
            DateTimeOffset.UtcNow,
            null,
            null));
        await persistence.UpsertAttemptAsync(new AttemptRecord(
            attempt.Value,
            transition.Value,
            instance.Value,
            run.Value,
            1,
            DateTimeOffset.UtcNow,
            null,
            null,
            "policy_test"));
        return new CanonicalCausalContext(workspace, run, instance, transition, attempt);
    }

    private static TransitionRuntimeRequest Request(CanonicalCausalContext causality)
    {
        var execution = new CanonicalTransitionExecutionContext(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            causality.Workspace,
            causality.Run,
            causality.WorkflowInstance,
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test"));
        return new TransitionRuntimeRequest(
            WorkflowIdentity.Plan,
            new WorkflowStageIdentity("Planning"),
            new WorkflowTransitionIdentity("WritePlan"),
            execution,
            FreshAttemptAuthorization.Instance);
    }

    private static RenderedPromptFact PromptFact(CanonicalCausalContext causality, string content = "rendered")
    {
        return new RenderedPromptFact(
            RenderedPromptFactIdentity.New(),
            causality,
            content,
            RenderedPromptFact.ComputeContentHash(content),
            new PromptTemplateIdentity("template"),
            "source-hash",
            new PolicyIdentity("policy_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test"),
            ConsumedInputManifestIdentity.New(),
            [new ConsumedInputFile("plan.md", new string('a', 64))],
            DateTimeOffset.UtcNow);
    }

    private static WorkflowTransitionDefinition Definition(bool withEffect) => new(
        new WorkflowTransitionIdentity("WritePlan"),
        "write plan",
        [],
        new GateDefinition(new GateIdentity("input"), "input", [], "test", "fail"),
        "WritePlan",
        ExecutionPosture.OneShotAgentPrompt,
        [],
        new GateDefinition(new GateIdentity("output"), "output", [], "test", "fail"),
        [],
        withEffect
            ? [new EffectDefinition(new EffectIdentity("persist"), EffectCategory.ProductPersistence,
                "validated", [], [], 1, "retry")]
            : [],
        [], [],
        new RecoveryDefinition("recovery", "recover", ["retry"], []));

    private static ProductRecord Product(CanonicalCausalContext causality) => new(
        new ProductIdentity("plan"),
        WorkflowIdentity.Plan,
        new WorkflowTransitionIdentity("WritePlan"),
        [WorkflowIdentity.Execute],
        "repository",
        "test",
        ["plan.md"],
        causality.Attempt.Value,
        ProductFreshness.Fresh,
        ProductValidationState.Valid,
        ProductLifecycle.Active,
        ["plan.md"]);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-transition-persistence-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }
}

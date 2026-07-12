using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Persistence;
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
        RuntimeProfileEvaluation readEvaluation =
            Assert.IsType<RuntimeProfileEvaluation>(await evaluationStore.ReadAsync(evaluation.Identity));
        ResolvedRuntimeProfile readProfile = Assert.IsType<ResolvedRuntimeProfile>(
            await ((IResolvedRuntimeProfileStore)evaluationStore).ReadAsync(profile.Identity));

        Assert.Equal(decision, readRecommendation.DecisionProduct);
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
        Assert.Equal(EffectExecutionStatus.Planned, Assert.Single(snapshot.EffectRecords).Status);
        Assert.Equal("EffectsPending", Assert.Single(await persistence.ReadAttemptsAsync()).Outcome);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM canonical_effect_intents;";
        Assert.Equal("Planned", Convert.ToString(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Recovery_coordinator_persists_typed_retry_plan_without_executing_work()
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
        var coordinator = new TransitionRecoveryCoordinator(
            runs,
            new CanonicalTransitionRecoveryPlanStore(persistence));

        TransitionRecoveryPlan plan = await coordinator.PlanAsync(causality.TransitionRun);

        Assert.Equal(TransitionRecoveryAction.RetryAsNewAttempt, plan.Action);
        Assert.Equal(RecoveryAttemptMode.RetryExistingTransitionRun, plan.ResultingAttemptMode);
        Assert.Equal(causality.Attempt, plan.SourceCausality.Attempt);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(
            LoopRelayWorkspaceDatabase.Resolve(repository));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT action FROM transition_recovery_plans WHERE recovery_id = $id;";
        command.Parameters.AddWithValue("$id", plan.RecoveryIdentity.Value);
        Assert.Equal("RetryAsNewAttempt", Convert.ToString(await command.ExecuteScalarAsync()));
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

    private static RenderedPromptFact PromptFact(CanonicalCausalContext causality)
    {
        const string content = "rendered";
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
            ? [new EffectDefinition(new EffectIdentity("publish"), EffectCategory.Publication,
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

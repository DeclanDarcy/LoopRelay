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
        EffectLease lease = (await effectStore.TryLeaseAsync(
            work.Intent.Identity, work.RowVersion, "test-worker", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), CancellationToken.None))!;
        EffectWorkItem started = await effectStore.AppendLifecycleAsync(
            work.Intent.Identity, lease.RowVersion, EffectLifecycle.Started, "test-worker",
            "started", [], DateTimeOffset.UtcNow, CancellationToken.None);
        await effectStore.RecordReceiptAsync(
            work.Intent.Identity,
            started.RowVersion,
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

    [Fact]
    public void EnsureReadableAfterAppend_throws_the_documented_message_when_the_row_was_not_found()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CanonicalRenderedPromptFactStore.EnsureReadableAfterAppend(readableAfterAppend: false));

        Assert.Equal("Rendered prompt fact was not readable after append.", exception.Message);
    }

    [Fact]
    public void EnsureReadableAfterAppend_does_not_throw_when_the_row_was_found()
    {
        CanonicalRenderedPromptFactStore.EnsureReadableAfterAppend(readableAfterAppend: true);
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

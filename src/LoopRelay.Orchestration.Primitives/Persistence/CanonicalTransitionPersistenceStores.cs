using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalTransitionRunStore(CanonicalWorkflowPersistenceStore _store) : ITransitionRunStore
{
    private static readonly JsonSerializerOptions RecoveryJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    public Task PersistStartedAsync(
        TransitionRunStarted started,
        CancellationToken cancellationToken) =>
        _store.UpsertTransitionRunAsync(
            new CanonicalTransitionRunRecord(
                started.Causality.TransitionRun.Value,
                started.Request.Workflow,
                started.Request.Stage,
                started.Definition.Identity,
                TransitionDurableState.Started,
                RuntimeOutcomeKind.Waiting,
                started.StartedAt,
                null,
                started.InputSnapshot.Hash,
                "Transition started.",
                [started.RenderedPrompt.PersistenceIdentity.Value, started.InputSnapshot.Hash]),
            cancellationToken);

    public async Task PersistStateAsync(
        TransitionRunStateUpdate update,
        CancellationToken cancellationToken)
    {
        CanonicalTransitionRunRecord existing = await ExistingOrFallbackAsync(update.Causality.TransitionRun.Value, update.Transition, cancellationToken);
        await _store.UpsertTransitionRunAsync(
            existing with
            {
                State = update.State,
                Outcome = OutcomeFor(update.State),
                CompletedAt = IsTerminal(update.State) ? update.RecordedAt : existing.CompletedAt,
                Explanation = update.Explanation,
                Evidence = update.Evidence,
            },
            cancellationToken);
    }

    public async Task PersistCompletedAsync(
        TransitionRunCompleted completed,
        CancellationToken cancellationToken)
    {
        CanonicalTransitionRunRecord existing = await ExistingOrFallbackAsync(
            completed.Causality.TransitionRun.Value,
            completed.Transition,
            cancellationToken);
        await _store.UpsertTransitionRunAsync(
            existing with
            {
                State = completed.Result.DurableState,
                Outcome = completed.Result.Outcome,
                CompletedAt = completed.CompletedAt,
                Explanation = completed.Result.Explanation,
                Evidence = completed.Result.Evidence,
            },
            cancellationToken);
    }

    public async Task<TransitionRunRecoverySnapshot?> LoadRecoveryAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot = await _store.LoadSnapshotAsync(cancellationToken);
        string runId = transitionRun.Value;
        CanonicalTransitionRunRecord? run = snapshot.TransitionRuns.SingleOrDefault(item => item.RunId == runId);
        if (run is null)
        {
            return null;
        }

        PromptExecutionResult? rawOutput = snapshot.TransitionEvidence
            .Where(item => item.RunId == runId && item.EventName == "RawPromptOutputCaptured")
            .OrderByDescending(item => item.EvidenceId)
            .Select(item => Deserialize<PromptExecutionResult>(item.DocumentJson))
            .FirstOrDefault(item => item is not null);
        TransitionBoundaryObservation[] boundaries = snapshot.TransitionEvidence
            .Where(item => item.RunId == runId && item.EventName == "TransitionBoundaryObserved")
            .OrderBy(item => item.EvidenceId)
            .Select(item => Deserialize<TransitionBoundaryObservation>(item.DocumentJson))
            .Where(item => item is not null)
            .Cast<TransitionBoundaryObservation>()
            .ToArray();
        EffectExecutionRecord[] effects = snapshot.EffectRecords
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.RecordId)
            .Select(item => new EffectExecutionRecord(item.Effect, item.Status, item.Explanation, item.Evidence))
            .ToArray();
        IReadOnlyList<AttemptRecord> attempts = await _store.ReadAttemptsAsync(cancellationToken);
        IReadOnlyList<WorkflowInstanceRecord> instances = await _store.ReadWorkflowInstancesAsync(cancellationToken);
        IReadOnlyList<RunRecord> rootRuns = await _store.ReadRunsAsync(cancellationToken);
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

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, RecoveryJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private async Task<CanonicalTransitionRunRecord> ExistingOrFallbackAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        CancellationToken cancellationToken)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot = await _store.LoadSnapshotAsync(cancellationToken);
        CanonicalTransitionRunRecord? existing = snapshot.TransitionRuns.FirstOrDefault(run => run.RunId == runId);
        return existing ?? new CanonicalTransitionRunRecord(
            runId,
            new WorkflowIdentity("Unknown"),
            new WorkflowStageIdentity("Unknown"),
            transition,
            TransitionDurableState.NotStarted,
            RuntimeOutcomeKind.Waiting,
            DateTimeOffset.UtcNow,
            null,
            null,
            "Transition run state was recorded before a start record was found.",
            []);
    }

    private static RuntimeOutcomeKind OutcomeFor(TransitionDurableState state) =>
        state switch
        {
            TransitionDurableState.Completed => RuntimeOutcomeKind.Completed,
            TransitionDurableState.Stalled => RuntimeOutcomeKind.Stalled,
            TransitionDurableState.InputUnsatisfied => RuntimeOutcomeKind.MissingRequiredInput,
            TransitionDurableState.Ambiguous => RuntimeOutcomeKind.Ambiguous,
            TransitionDurableState.Failed => RuntimeOutcomeKind.Failed,
            TransitionDurableState.Cancelled => RuntimeOutcomeKind.Cancelled,
            TransitionDurableState.EffectsPending => RuntimeOutcomeKind.EffectsPending,
            TransitionDurableState.ProviderOutcomeUnknown => RuntimeOutcomeKind.RecoveryRequired,
            TransitionDurableState.InputInvalidated => RuntimeOutcomeKind.InputInvalidated,
            TransitionDurableState.ConcurrentStateConflict => RuntimeOutcomeKind.ConcurrentStateConflict,
            _ => RuntimeOutcomeKind.Waiting,
        };

    private static bool IsTerminal(TransitionDurableState state) =>
        state is TransitionDurableState.Completed
            or TransitionDurableState.Stalled
            or TransitionDurableState.InputUnsatisfied
            or TransitionDurableState.Waiting
            or TransitionDurableState.Ambiguous
            or TransitionDurableState.Failed
            or TransitionDurableState.Cancelled;
}

public sealed class CanonicalTransitionBoundaryJournal(
    CanonicalWorkflowPersistenceStore _store,
    TransitionBoundaryKind? _interruptAt = null) : ITransitionBoundaryJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task RecordAsync(TransitionBoundaryObservation observation, CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                observation.Causality.TransitionRun.Value,
                observation.Transition,
                "TransitionBoundaryObserved",
                observation.ObservedAt,
                StateFor(observation.Boundary),
                $"Observed transition boundary {observation.Boundary}.",
                observation.Evidence.Append($"boundary:{observation.Boundary}").ToArray(),
                JsonSerializer.Serialize(observation, JsonOptions)),
            cancellationToken);

    public bool ShouldInterrupt(TransitionBoundaryObservation observation) =>
        _interruptAt == observation.Boundary;

    private static TransitionDurableState StateFor(TransitionBoundaryKind boundary) => boundary switch
    {
        TransitionBoundaryKind.ProviderCompleted or TransitionBoundaryKind.RawOutputPersisted => TransitionDurableState.PromptCompleted,
        TransitionBoundaryKind.OutputInterpreted => TransitionDurableState.OutputInterpreted,
        TransitionBoundaryKind.OutputValidated => TransitionDurableState.OutputValidated,
        TransitionBoundaryKind.DuringEffects => TransitionDurableState.EffectsPartiallyApplied,
        TransitionBoundaryKind.EffectsApplied => TransitionDurableState.EffectsApplied,
        TransitionBoundaryKind.CompletionPersisted => TransitionDurableState.Completed,
        _ => TransitionDurableState.Started,
    };
}

public sealed class CanonicalTransitionEvidenceStore(CanonicalWorkflowPersistenceStore _store) : ITransitionEvidenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task RecordEventAsync(
        TransitionEvidenceEvent evidence,
        CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                evidence.Causality.TransitionRun.Value,
                evidence.Transition,
                evidence.EventName,
                evidence.RecordedAt,
                evidence.State,
                evidence.Explanation,
                evidence.Evidence,
                JsonSerializer.Serialize(evidence, JsonOptions)),
            cancellationToken);

    public Task RecordRawOutputAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        PromptExecutionResult executionResult,
        CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                causality.TransitionRun.Value,
                transition,
                "RawPromptOutputCaptured",
                DateTimeOffset.UtcNow,
                TransitionDurableState.PromptCompleted,
                executionResult.FailureMessage ?? "Raw prompt output captured.",
                ["raw-output"],
                JsonSerializer.Serialize(executionResult, JsonOptions)),
            cancellationToken);

    public Task RecordFailureAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        string failure,
        CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                causality.TransitionRun.Value,
                transition,
                "TransitionFailure",
                DateTimeOffset.UtcNow,
                TransitionDurableState.Failed,
                failure,
                ["failure"],
                JsonSerializer.Serialize(new { failure }, JsonOptions)),
            cancellationToken);
}

public sealed class CanonicalTransitionWarningStore(CanonicalWorkflowPersistenceStore _store) : ITransitionWarningStore
{
    public Task RecordWarningAsync(
        TransitionWarningCapture warning,
        CancellationToken cancellationToken) =>
        _store.AppendWarningAsync(
            new CanonicalWarningRecord(
                CausalUlid.NewId("warn"),
                warning.Request.Workflow,
                warning.Request.Stage,
                warning.Transition,
                warning.Category,
                warning.Concern,
                "canonical transition runtime",
                warning.Remediation,
                warning.Evidence,
                warning.RecordedAt,
                warning.Causality.TransitionRun.Value),
            cancellationToken);
}

public sealed class CanonicalTransitionRecoveryStore(CanonicalWorkflowPersistenceStore _store) : ITransitionRecoveryStore
{
    public Task RecordRecoveryMarkerAsync(
        TransitionRecoveryMarkerCapture marker,
        CancellationToken cancellationToken) =>
        _store.UpsertRecoveryMarkerAsync(
            new CanonicalRecoveryMarkerRecord(
                $"{marker.Causality.TransitionRun.Value}:{marker.Transition.Value}:{marker.DurableState}",
                marker.Request.Workflow,
                marker.Request.Stage,
                marker.Transition,
                marker.Recovery,
                marker.Evidence,
                marker.RecordedAt),
            cancellationToken);
}

public sealed class CanonicalTransitionGateEvaluationStore(CanonicalWorkflowPersistenceStore _store) : ITransitionGateEvaluationStore
{
    public Task RecordGateEvaluationAsync(
        TransitionGateEvaluationCapture evaluation,
        CancellationToken cancellationToken) =>
        _store.AppendGateEvaluationAsync(
            new CanonicalGateEvaluationRecord(
                0,
                evaluation.Request.Workflow,
                evaluation.Request.Stage,
                evaluation.Transition,
                evaluation.Gate.Identity,
                evaluation.Result.Status,
                evaluation.EvaluatedAt,
                evaluation.Result.Requirements,
                evaluation.Result.Explanation,
                evaluation.Result.Evidence,
                evaluation.Causality.TransitionRun.Value),
            cancellationToken);
}

public sealed class CanonicalAttemptStore(CanonicalWorkflowPersistenceStore _store) : IAttemptStore
{
    public Task PersistAttemptStartedAsync(
        AttemptRecord attempt,
        CancellationToken cancellationToken) =>
        _store.UpsertAttemptAsync(attempt, cancellationToken);

    public Task PersistAttemptCompletedAsync(
        AttemptIdentity attempt,
        DateTimeOffset completedAt,
        string outcome,
        CancellationToken cancellationToken) =>
        _store.CompleteAttemptAsync(attempt.Value, completedAt, outcome, cancellationToken);
}

public sealed class CanonicalWorkflowInstanceRecorder(CanonicalWorkflowPersistenceStore _store) : IWorkflowInstanceRecorder
{
    public async Task<WorkflowInstanceIdentity> BeginInstanceAsync(
        RunIdentity run,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken)
    {
        WorkflowInstanceIdentity workflowInstance = WorkflowInstanceIdentity.New();
        await _store.UpsertWorkflowInstanceAsync(
            new WorkflowInstanceRecord(
                workflowInstance.Value,
                run.Value,
                workflow,
                string.Empty,
                "Active",
                DateTimeOffset.UtcNow,
                null,
                null),
            cancellationToken);
        return workflowInstance;
    }

    public Task CompleteInstanceAsync(
        WorkflowInstanceIdentity workflowInstance,
        string status,
        string? outcome,
        CancellationToken cancellationToken) =>
        _store.CompleteWorkflowInstanceAsync(
            workflowInstance.Value,
            status,
            outcome,
            DateTimeOffset.UtcNow,
            cancellationToken);
}

public sealed class CanonicalTransitionEffectStore(CanonicalWorkflowPersistenceStore _store) : ITransitionEffectStore
{
    public Task RecordEffectAsync(
        TransitionEffectRecordCapture effect,
        CancellationToken cancellationToken) =>
        _store.AppendEffectRecordAsync(
            new CanonicalEffectRecord(
                0,
                effect.Causality.TransitionRun.Value,
                effect.Effect,
                effect.Category,
                effect.Status,
                effect.RecordedAt,
                effect.Explanation,
                effect.Evidence),
            cancellationToken);
}

public sealed class CanonicalTransitionCommitStore(CanonicalWorkflowPersistenceStore _store) : ITransitionCommitStore
{
    public Task CommitAsync(TransitionCommitCapture capture, CancellationToken cancellationToken) =>
        _store.CommitTransitionAsync(capture, cancellationToken);
}

public sealed class CanonicalCandidateProductStore(CanonicalWorkflowPersistenceStore _store) : ICandidateProductStore
{
    public async Task RegisterAsync(
        CanonicalCausalContext causality,
        IReadOnlyList<ProductRecord> candidates,
        CancellationToken cancellationToken)
    {
        foreach (ProductRecord candidate in candidates)
        {
            await _store.UpsertProductAsync(
                candidate with
                {
                    CausalIdentity = causality.Attempt.Value,
                    Freshness = ProductFreshness.Unknown,
                    ValidationState = ProductValidationState.Unknown,
                    Lifecycle = ProductLifecycle.Proposed,
                },
                cancellationToken);
        }
    }
}

public sealed class CanonicalRenderedPromptFactStore(CanonicalWorkflowPersistenceStore _store)
    : IRenderedPromptStore, IRenderedPromptFactReader
{
    private readonly ConcurrentDictionary<RenderedPromptFactIdentity, PersistedRenderedPromptFact> appended = new();

    public async Task<PersistedRenderedPromptFact> AppendAsync(
        RenderedPromptFact fact,
        CancellationToken cancellationToken)
    {
        RenderedPromptPersistenceIdentity persistenceIdentity = RenderedPromptPersistenceIdentity.New();
        var record = new CanonicalRenderedPromptRecord(
            fact.Identity.Value,
            fact.Causality.TransitionRun.Value,
            fact.Causality.Attempt.Value,
            fact.TemplateIdentity.Value,
            fact.TemplateSourceHash,
            fact.ContentHash,
            fact.RenderedContent,
            fact.ConsumedInputs.Select(input => new CanonicalReadReceiptFile(input.Path, input.Sha256)).ToArray(),
            fact.PolicyIdentity.Value,
            fact.RenderedAt,
            PersistenceId: persistenceIdentity.Value,
            PromptPolicyProfileId: fact.PolicyProfileIdentity.Value,
            ConsumedInputManifestId: fact.ConsumedInputManifestIdentity.Value,
            RenderedEncoding: fact.RenderedEncoding);
        await _store.AppendRenderedPromptAsync(record, cancellationToken);
        IReadOnlyList<CanonicalRenderedPromptRecord> records =
            await _store.ReadRenderedPromptsAsync(cancellationToken);
        int index = records.ToList().FindIndex(item => item.RenderedPromptId == fact.Identity.Value);
        if (index < 0)
        {
            throw new InvalidOperationException("Rendered prompt fact was not readable after append.");
        }

        var persisted = new PersistedRenderedPromptFact(
            fact,
            persistenceIdentity,
            index + 1,
            DateTimeOffset.UtcNow);
        appended[fact.Identity] = persisted;
        return persisted;
    }

    public async Task<PersistedRenderedPromptFact?> ReadAsync(
        RenderedPromptFactIdentity prompt,
        CancellationToken cancellationToken)
    {
        if (appended.TryGetValue(prompt, out PersistedRenderedPromptFact? persisted))
        {
            return persisted;
        }

        IReadOnlyList<CanonicalRenderedPromptRecord> prompts =
            await _store.ReadRenderedPromptsAsync(cancellationToken);
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

        IReadOnlyList<AttemptRecord> attempts = await _store.ReadAttemptsAsync(cancellationToken);
        AttemptRecord? attempt = attempts.SingleOrDefault(item => item.AttemptId == record.AttemptId);
        if (attempt is null)
        {
            return null;
        }

        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(await _store.ReadWorkspaceIdentityAsync(cancellationToken)),
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
}

public sealed class CanonicalPromptDispatchLifecycleStore(CanonicalWorkflowPersistenceStore _store)
    : IPromptDispatchLifecycleStore
{
    public Task AppendAsync(PromptDispatchLifecycleEvent dispatchEvent, CancellationToken cancellationToken)
    {
        CanonicalCausalContext causality = dispatchEvent.Causality;
        return _store.AppendPromptDispatchEventAsync(
            new CanonicalPromptDispatchEventRecord(
                0,
                dispatchEvent.Dispatch.Value,
                dispatchEvent.Prompt.Value,
                dispatchEvent.Persistence.Value,
                causality.Workspace.Value,
                causality.Run.Value,
                causality.WorkflowInstance.Value,
                causality.TransitionRun.Value,
                causality.Attempt.Value,
                dispatchEvent.RuntimeProfile.Value,
                dispatchEvent.Session?.Value,
                dispatchEvent.Turn?.Value,
                dispatchEvent.State,
                dispatchEvent.RecordedAt,
                dispatchEvent.Evidence),
            cancellationToken);
    }
}

public sealed class CanonicalTransitionRecoveryPlanStore(CanonicalWorkflowPersistenceStore _store)
    : ITransitionRecoveryPlanStore
{
    public Task PersistAsync(TransitionRecoveryPlan plan, CancellationToken cancellationToken) =>
        _store.AppendRecoveryPlanAsync(plan, cancellationToken);
}

public sealed class CanonicalChainBoundaryEvidenceStore(CanonicalWorkflowPersistenceStore _store)
    : IChainBoundaryEvidenceStore
{
    public Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken)
    {
        WorkflowBoundaryEvaluation evaluation = capture.Evaluation;
        string[] evidence = evaluation.ExitGate.Evidence
            .Concat(evaluation.EntryGate?.Evidence ?? [])
            .Concat(evaluation.ProductTransfer?.Gate.Evidence ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return _store.AppendChainBoundaryEventAsync(
            new CanonicalChainBoundaryEventRecord(
                CausalUlid.NewId("bnd"),
                capture.Run.Value,
                capture.ChainIdentity,
                evaluation.SourceWorkflow,
                evaluation.TargetWorkflow,
                evaluation.ExitGate.Status,
                evaluation.EntryGate?.Status,
                evaluation.ProductTransfer?.Gate.Status,
                evaluation.CanAdvance ? "Advanced" : "StoppedAtBoundary",
                evaluation.Explanation,
                evidence,
                JsonSerializer.Serialize(evaluation),
                capture.RecordedAt),
            cancellationToken);
    }
}

public sealed class CanonicalTransitionEffectIntentStateStore(CanonicalWorkflowPersistenceStore _store)
    : ITransitionEffectIntentStateStore
{
    public Task RecordStateAsync(
        TransitionRunIdentity transitionRun,
        EffectIdentity effect,
        EffectExecutionStatus status,
        string? failure,
        CancellationToken cancellationToken) =>
        _store.RecordEffectIntentStateAsync(
            transitionRun, effect, status, failure, cancellationToken);
}

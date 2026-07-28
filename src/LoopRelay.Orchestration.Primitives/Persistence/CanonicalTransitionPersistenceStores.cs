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
        string runId = transitionRun.Value;
        CanonicalTransitionRunRecord? run = await _store.ReadTransitionRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        IReadOnlyList<CanonicalTransitionEvidenceRecord> evidence =
            await _store.ReadTransitionEvidenceByRunAsync(runId, cancellationToken);
        PromptExecutionResult? rawOutput = evidence
            .Where(item => item.EventName == "RawPromptOutputCaptured")
            .OrderByDescending(item => item.EvidenceId)
            .Select(item => Deserialize<PromptExecutionResult>(item.DocumentJson))
            .FirstOrDefault(item => item is not null);
        TransitionBoundaryObservation[] boundaries = evidence
            .Where(item => item.EventName == "TransitionBoundaryObserved")
            .OrderBy(item => item.EvidenceId)
            .Select(item => Deserialize<TransitionBoundaryObservation>(item.DocumentJson))
            .Where(item => item is not null)
            .Cast<TransitionBoundaryObservation>()
            .ToArray();
        EffectExecutionRecord[] effects = (await _store.ReadEffectRecordsByRunAsync(runId, cancellationToken))
            .OrderBy(item => item.RecordId)
            .Select(item => new EffectExecutionRecord(item.Effect, item.Status, item.Explanation, item.Evidence))
            .ToArray();
        AttemptRecord? attempt = await _store.ReadLatestAttemptByTransitionRunAsync(runId, cancellationToken);
        WorkflowInstanceRecord? instance = attempt is null
            ? null
            : await _store.ReadWorkflowInstanceAsync(attempt.WorkflowInstanceId, cancellationToken);
        RunRecord? rootRun = attempt is null
            ? null
            : await _store.ReadRunAsync(attempt.RunId, cancellationToken);
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
        CanonicalTransitionRunRecord? existing = await _store.ReadTransitionRunAsync(runId, cancellationToken);
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

public sealed class CanonicalWorkflowInstanceRecorder(
    CanonicalWorkflowPersistenceStore _store,
    CanonicalWorkflowCatalogSnapshot? _catalog = null) : IWorkflowInstanceRecorder
{
    public async Task<WorkflowInstanceIdentity> BeginInstanceAsync(
        RunIdentity run,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkflowInstanceRecord> active = await _store.ReadActiveWorkflowInstancesAsync(
            run.Value, workflow.Value, cancellationToken);
        if (active.Count > 1)
            throw new InvalidOperationException(
                $"Multiple active workflow instances exist for root '{run}' and workflow '{workflow}'.");
        if (active.Count == 1)
            return new WorkflowInstanceIdentity(active[0].WorkflowInstanceId);
        WorkflowInstanceIdentity workflowInstance = WorkflowInstanceIdentity.New();
        await _store.UpsertWorkflowInstanceAsync(
            new WorkflowInstanceRecord(
                workflowInstance.Value,
                run.Value,
                workflow,
                (_catalog ?? CanonicalWorkflowCatalog.Current).SemanticVersion,
                "Active",
                DateTimeOffset.UtcNow,
                null,
                null,
                (_catalog ?? CanonicalWorkflowCatalog.Current).Identity),
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
        long ledgerSequence = await _store.AppendRenderedPromptAsync(record, cancellationToken);

        var persisted = new PersistedRenderedPromptFact(
            fact,
            persistenceIdentity,
            ledgerSequence,
            DateTimeOffset.UtcNow);
        appended[fact.Identity] = persisted;
        return persisted;
    }

    /// <summary>
    /// Reads a rendered-prompt fact by identity. Used to call
    /// <see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptsAsync"/> - every rendered
    /// prompt in the workspace, including every other prompt's full <c>RenderedText</c> - and find
    /// the wanted one with <c>FindIndex</c>, computing the ledger position from that same index
    /// (Task 3.2). It now reads the row by key
    /// (<see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptAsync"/>) and passes that
    /// read's own <c>rowid</c> straight into the ledger position read
    /// (<see cref="CanonicalWorkflowPersistenceStore.ReadRenderedPromptLedgerPositionAsync"/>, fix
    /// pass 1, finding 3), neither of which loads any other row's <c>RenderedText</c>. The attempt
    /// lookup below still hydrates every attempt in the workspace - out of scope for this task, since
    /// it carries no large field and the brief's SQL shape contract does not cover it; noted as a
    /// tangle, not fixed here.
    /// </summary>
    public async Task<PersistedRenderedPromptFact?> ReadAsync(
        RenderedPromptFactIdentity prompt,
        CancellationToken cancellationToken)
    {
        if (appended.TryGetValue(prompt, out PersistedRenderedPromptFact? persisted))
        {
            return persisted;
        }

        (CanonicalRenderedPromptRecord? record, long rowId) =
            await _store.ReadRenderedPromptAsync(prompt.Value, cancellationToken);
        if (record is null)
        {
            return null;
        }

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

        long ledgerPosition =
            await _store.ReadRenderedPromptLedgerPositionAsync(rowId, cancellationToken);

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
            ledgerPosition,
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

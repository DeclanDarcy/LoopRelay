using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalTransitionRunStore(CanonicalWorkflowPersistenceStore _store) : ITransitionRunStore
{
    public Task PersistStartedAsync(
        TransitionRunStarted started,
        CancellationToken cancellationToken) =>
        _store.UpsertTransitionRunAsync(
            new CanonicalTransitionRunRecord(
                started.RunId,
                started.Request.Workflow,
                started.Request.Stage,
                started.Definition.Identity,
                TransitionDurableState.Started,
                RuntimeOutcomeKind.Waiting,
                started.StartedAt,
                null,
                started.InputSnapshot.Hash,
                "Transition started.",
                [started.RenderedPrompt.EvidenceLocation, started.InputSnapshot.Hash]),
            cancellationToken);

    public async Task PersistStateAsync(
        TransitionRunStateUpdate update,
        CancellationToken cancellationToken)
    {
        CanonicalTransitionRunRecord existing = await ExistingOrFallbackAsync(update.RunId, update.Transition, cancellationToken);
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
            completed.RunId,
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
                evidence.RunId,
                evidence.Transition,
                evidence.EventName,
                evidence.RecordedAt,
                evidence.State,
                evidence.Explanation,
                evidence.Evidence,
                JsonSerializer.Serialize(evidence, JsonOptions)),
            cancellationToken);

    public Task RecordRawOutputAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        PromptExecutionResult executionResult,
        CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                runId,
                transition,
                "RawPromptOutputCaptured",
                DateTimeOffset.UtcNow,
                TransitionDurableState.PromptCompleted,
                executionResult.FailureMessage ?? "Raw prompt output captured.",
                ["raw-output"],
                JsonSerializer.Serialize(executionResult, JsonOptions)),
            cancellationToken);

    public Task RecordFailureAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        string failure,
        CancellationToken cancellationToken) =>
        _store.AppendTransitionEvidenceAsync(
            new CanonicalTransitionEvidenceRecord(
                0,
                runId,
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
                warning.RunId),
            cancellationToken);
}

public sealed class CanonicalTransitionRecoveryStore(CanonicalWorkflowPersistenceStore _store) : ITransitionRecoveryStore
{
    public Task RecordRecoveryMarkerAsync(
        TransitionRecoveryMarkerCapture marker,
        CancellationToken cancellationToken) =>
        _store.UpsertRecoveryMarkerAsync(
            new CanonicalRecoveryMarkerRecord(
                $"{marker.RunId}:{marker.Transition.Value}:{marker.DurableState}",
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
                evaluation.RunId),
            cancellationToken);
}

public sealed class CanonicalAttemptStore(CanonicalWorkflowPersistenceStore _store) : IAttemptStore
{
    public Task PersistAttemptStartedAsync(
        AttemptRecord attempt,
        CancellationToken cancellationToken) =>
        _store.UpsertAttemptAsync(attempt, cancellationToken);

    public Task PersistAttemptCompletedAsync(
        string attemptId,
        DateTimeOffset completedAt,
        string outcome,
        CancellationToken cancellationToken) =>
        _store.CompleteAttemptAsync(attemptId, completedAt, outcome, cancellationToken);
}

public sealed class CanonicalWorkflowInstanceRecorder(CanonicalWorkflowPersistenceStore _store) : IWorkflowInstanceRecorder
{
    public async Task<string> BeginInstanceAsync(
        string runId,
        WorkflowIdentity workflow,
        CancellationToken cancellationToken)
    {
        string workflowInstanceId = WorkflowInstanceIdentity.New().Value;
        await _store.UpsertWorkflowInstanceAsync(
            new WorkflowInstanceRecord(
                workflowInstanceId,
                runId,
                workflow,
                string.Empty,
                "Active",
                DateTimeOffset.UtcNow,
                null,
                null),
            cancellationToken);
        return workflowInstanceId;
    }

    public Task CompleteInstanceAsync(
        string workflowInstanceId,
        string status,
        string? outcome,
        CancellationToken cancellationToken) =>
        _store.CompleteWorkflowInstanceAsync(
            workflowInstanceId,
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
                effect.RunId,
                effect.Effect,
                effect.Category,
                effect.Status,
                effect.RecordedAt,
                effect.Explanation,
                effect.Evidence),
            cancellationToken);
}

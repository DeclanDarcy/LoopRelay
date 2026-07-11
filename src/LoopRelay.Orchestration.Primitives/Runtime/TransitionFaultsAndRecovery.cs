using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public enum TransitionBoundaryKind
{
    PreResolution,
    InputGateSatisfied,
    PromptRendered,
    PreSubmission,
    RequestWriteStarted,
    RequestSubmitted,
    RequestAccepted,
    ProviderTurnIdentified,
    PartialOutput,
    ProviderTerminal,
    ProviderCompleted,
    RawOutputPersisted,
    OutputInterpreted,
    OutputValidated,
    BeforeEffects,
    DuringEffects,
    EffectsApplied,
    CompletionPersisted,
    Reobserved,
    ChainTransfer,
}

public enum TransitionRecoveryDisposition
{
    SafeRetry,
    ReconcileProvider,
    MaterializeCommittedOutput,
    ApplyVerifiedEffects,
    CompleteWithoutWork,
    OperatorUnblock,
    Cancelled,
    FailClosedUnknownSideEffect,
    NonRecoverableCorruption,
    ReuseCompleted,
}

public sealed record TransitionBoundaryObservation(
    string RunId,
    WorkflowTransitionIdentity Transition,
    TransitionBoundaryKind Boundary,
    int Sequence,
    DateTimeOffset ObservedAt,
    string InputSnapshotHash,
    string? ProviderTurnId,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRecoveryDecision(
    TransitionRecoveryDisposition Disposition,
    bool MaySubmitProviderTurn,
    bool MayApplyEffects,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed class TransitionFaultInjectedException(
    TransitionBoundaryObservation observation) : Exception($"Certification fault injected at {observation.Boundary}.")
{
    public TransitionBoundaryObservation Observation { get; } = observation;
}

public interface ITransitionBoundaryJournal
{
    Task RecordAsync(TransitionBoundaryObservation observation, CancellationToken cancellationToken);

    bool ShouldInterrupt(TransitionBoundaryObservation observation) => false;
}

public static class TransitionRecoveryClassifier
{
    public static TransitionRecoveryDecision Classify(TransitionRunRecoverySnapshot snapshot)
    {
        string[] boundaryEvidence = snapshot.Boundaries
            .OrderBy(item => item.Sequence)
            .Select(item => $"boundary:{item.Boundary}")
            .ToArray();
        if (snapshot.State == TransitionDurableState.Completed)
        {
            return Decision(TransitionRecoveryDisposition.ReuseCompleted, false, false,
                "The transition is already complete; return durable completion without model or effect work.");
        }

        if (snapshot.State == TransitionDurableState.EffectsApplied)
        {
            return Decision(TransitionRecoveryDisposition.CompleteWithoutWork, false, false,
                "All ordered effects are durable; persist completion without repeating them.");
        }

        if (snapshot.State == TransitionDurableState.EffectsPartiallyApplied ||
            snapshot.Boundaries.Any(item => item.Boundary == TransitionBoundaryKind.DuringEffects))
        {
            return Decision(TransitionRecoveryDisposition.FailClosedUnknownSideEffect, false, false,
                "An effect may have occurred without complete durable verification; operator reconciliation is required.");
        }

        if (snapshot.RawOutput is not null || snapshot.State is TransitionDurableState.PromptCompleted
            or TransitionDurableState.OutputInterpreted or TransitionDurableState.OutputValidated)
        {
            return Decision(TransitionRecoveryDisposition.MaterializeCommittedOutput, false, true,
                "Committed raw provider output is available; continue deterministic interpretation, validation, and effects.");
        }

        bool submitted = snapshot.Boundaries.Any(item => item.Boundary is TransitionBoundaryKind.RequestSubmitted
            or TransitionBoundaryKind.RequestAccepted or TransitionBoundaryKind.ProviderTurnIdentified
            or TransitionBoundaryKind.ProviderTerminal or TransitionBoundaryKind.ProviderCompleted);
        if (submitted)
        {
            return Decision(TransitionRecoveryDisposition.ReconcileProvider, false, false,
                "The provider request may have been accepted; reconcile the provider turn before any resubmission.");
        }

        bool preSubmission = snapshot.Boundaries.Any(item => item.Boundary is TransitionBoundaryKind.PreSubmission
            or TransitionBoundaryKind.RequestWriteStarted);
        if (preSubmission && snapshot.State is TransitionDurableState.Started or TransitionDurableState.Cancelled)
        {
            return Decision(TransitionRecoveryDisposition.SafeRetry, true, true,
                "Durable evidence proves submission did not occur; retrying the same run is safe.");
        }

        return Decision(TransitionRecoveryDisposition.OperatorUnblock, false, false,
            "Recovery evidence is insufficient to authorize provider or effect work.");

        TransitionRecoveryDecision Decision(
            TransitionRecoveryDisposition disposition,
            bool provider,
            bool effects,
            string explanation) =>
            new(disposition, provider, effects, explanation, boundaryEvidence.Append($"state:{snapshot.State}").ToArray());
    }
}

public sealed class FaultInjectingPromptExecutor(
    IPromptExecutor _inner,
    ITransitionEvidenceStore _evidence,
    ITransitionBoundaryJournal _journal) : IPromptExecutor
{
    public async Task<PromptExecutionResult> ExecuteAsync(
        PromptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var progress = new DurableProgress(request, _journal);
        progress.Observe(TransitionBoundaryKind.PreSubmission);
        using IDisposable scope = AgentTurnProgress.Use(progress);
        PromptExecutionResult result = await _inner.ExecuteAsync(request, cancellationToken);
        await _evidence.RecordRawOutputAsync(
            request.RunId,
            request.Transition,
            result,
            CancellationToken.None);
        progress.Observe(TransitionBoundaryKind.ProviderCompleted);
        return result with
        {
            Metadata = new Dictionary<string, string>(result.Metadata, StringComparer.Ordinal)
            {
                ["durable-raw-output"] = "true",
            },
        };
    }

    private sealed class DurableProgress(
        PromptExecutionRequest request,
        ITransitionBoundaryJournal journal) : ICriticalAgentTurnProgressObserver
    {
        private int sequence;

        public void RequestWriteStarted() => Observe(TransitionBoundaryKind.RequestWriteStarted);
        public void RequestSubmitted() => Observe(TransitionBoundaryKind.RequestSubmitted);
        public void RequestAccepted() => Observe(TransitionBoundaryKind.RequestAccepted);
        public void FirstProtocolEvent() { }
        public void FirstOutput() => Observe(TransitionBoundaryKind.PartialOutput);
        public void ProviderTurnIdentified(string providerTurnId) => Observe(TransitionBoundaryKind.ProviderTurnIdentified, providerTurnId);
        public void Terminal() => Observe(TransitionBoundaryKind.ProviderTerminal);
        public void Unknown() { }

        public void Observe(TransitionBoundaryKind boundary, string? providerTurnId = null)
        {
            var observation = new TransitionBoundaryObservation(
                request.RunId,
                request.Transition,
                boundary,
                Interlocked.Increment(ref sequence),
                DateTimeOffset.UtcNow,
                request.InputSnapshotHash,
                providerTurnId,
                [$"prompt:{request.RenderedPrompt.PromptIdentity}"]);
            journal.RecordAsync(observation, CancellationToken.None).GetAwaiter().GetResult();
            if (journal.ShouldInterrupt(observation))
            {
                throw new TransitionFaultInjectedException(observation);
            }
        }
    }
}

using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public enum TransitionBoundaryKind
{
    PreResolution,
    InputGateSatisfied,
    PromptRendered,
    DispatchIntended,
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
    Cancelled,
    FailClosedUnknownSideEffect,
    NonRecoverableCorruption,
    ReuseCompleted,
}

public sealed record TransitionBoundaryObservation(
    CanonicalCausalContext Causality,
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

public enum RecoveryAttemptMode
{
    NoAttempt,
    ContinueDurableAttempt,
    DeterministicContinuation,
    RetryExistingTransitionRun,
    FreshTransitionRun,
}

public sealed record TransitionRecoveryPlan(
    RecoveryAttemptIdentity RecoveryIdentity,
    CanonicalCausalContext SourceCausality,
    string Classification,
    CanonicalRecoveryAction Action,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Preconditions,
    RecoveryAttemptMode ResultingAttemptMode,
    int NextAttemptIndex);

public interface ITransitionRecoveryPlanStore
{
    Task PersistAsync(TransitionRecoveryPlan plan, CancellationToken cancellationToken);
}

public interface ITransitionRecoveryCoordinator
{
    Task<TransitionRecoveryPlan> PlanAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Recovery Authority classifies durable prior-attempt evidence and persists a typed plan. It does
/// not execute provider work, effects, or workflow progression.
/// </summary>
public sealed class TransitionRecoveryCoordinator(
    ITransitionRunStore _runs,
    ITransitionRecoveryPlanStore _plans) : ITransitionRecoveryCoordinator
{
    public async Task<TransitionRecoveryPlan> PlanAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken = default)
    {
        if (transitionRun.IsEmpty)
        {
            throw new ArgumentException("Transition-run identity must not be empty.", nameof(transitionRun));
        }

        TransitionRunRecoverySnapshot snapshot = await _runs.LoadRecoveryAsync(transitionRun, cancellationToken)
            ?? throw new InvalidOperationException($"No durable recovery evidence exists for '{transitionRun}'.");
        TransitionRecoveryDecision decision = TransitionRecoveryClassifier.Classify(snapshot);
        (CanonicalRecoveryAction action, RecoveryAttemptMode mode, string[] preconditions) =
            Map(decision.Disposition);
        bool cancelledBeforeDispatch = decision.Disposition == TransitionRecoveryDisposition.Cancelled &&
            !snapshot.Boundaries.Any(item => item.Boundary is TransitionBoundaryKind.RequestWriteStarted or
                TransitionBoundaryKind.RequestSubmitted or TransitionBoundaryKind.RequestAccepted or
                TransitionBoundaryKind.ProviderTurnIdentified);
        if (cancelledBeforeDispatch)
        {
            action = CanonicalRecoveryAction.RetryNewAttempt;
            mode = RecoveryAttemptMode.RetryExistingTransitionRun;
            preconditions = ["cancellation is durable", "outward submission is proven absent"];
        }
        var plan = new TransitionRecoveryPlan(
            RecoveryAttemptIdentity.New(),
            snapshot.Causality,
            decision.Disposition.ToString(),
            action,
            decision.Evidence,
            preconditions,
            mode,
            mode == RecoveryAttemptMode.RetryExistingTransitionRun ? 2 : 1);
        await _plans.PersistAsync(plan, cancellationToken);
        return plan;
    }

    private static (CanonicalRecoveryAction Action, RecoveryAttemptMode Mode, string[] Preconditions) Map(
        TransitionRecoveryDisposition disposition) => disposition switch
    {
        TransitionRecoveryDisposition.ReuseCompleted =>
            (CanonicalRecoveryAction.Wait, RecoveryAttemptMode.NoAttempt, ["canonical completion remains authoritative"]),
        TransitionRecoveryDisposition.CompleteWithoutWork or TransitionRecoveryDisposition.ApplyVerifiedEffects =>
            (CanonicalRecoveryAction.ReconcileEffects, RecoveryAttemptMode.NoAttempt, ["effect receipts verify postconditions"]),
        TransitionRecoveryDisposition.MaterializeCommittedOutput =>
            (CanonicalRecoveryAction.ReuseRawOutput, RecoveryAttemptMode.DeterministicContinuation, ["raw outcome hash is verified"]),
        TransitionRecoveryDisposition.ReconcileProvider =>
            (CanonicalRecoveryAction.ReconcileProvider, RecoveryAttemptMode.NoAttempt, ["provider correlation is available"]),
        TransitionRecoveryDisposition.SafeRetry =>
            (CanonicalRecoveryAction.RetryNewAttempt, RecoveryAttemptMode.RetryExistingTransitionRun, ["submission is proven not accepted"]),
        TransitionRecoveryDisposition.FailClosedUnknownSideEffect =>
            (CanonicalRecoveryAction.ReconcileEffects, RecoveryAttemptMode.NoAttempt, ["effect outcome must be reconciled"]),
        TransitionRecoveryDisposition.Cancelled =>
            (CanonicalRecoveryAction.Wait, RecoveryAttemptMode.NoAttempt, ["cancellation evidence is durable"]),
        _ =>
            (CanonicalRecoveryAction.RequestHumanDecision, RecoveryAttemptMode.NoAttempt, ["recovery evidence is insufficient"]),
    };
}

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

        RecoveryDurableFacts facts = TransitionRecoveryFactProjector.Project(snapshot);
        var recoveryCase = new CanonicalRecoveryCase(
            new RecoveryCaseIdentity($"recoverycase:ProviderDispatch:{snapshot.Causality.Attempt.Value}"),
            facts.Scope,
            facts.Subject,
            snapshot.Boundaries.FirstOrDefault()?.ObservedAt ?? DateTimeOffset.UtcNow);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(recoveryCase, facts);
        return classification.Classification switch
        {
            RecoveryBoundaryClassification.PartiallyEffected =>
                Decision(TransitionRecoveryDisposition.FailClosedUnknownSideEffect, false, false,
                    "Required effects are partially settled and must be reconciled before progress."),
            RecoveryBoundaryClassification.SucceededUncommitted =>
                Decision(TransitionRecoveryDisposition.MaterializeCommittedOutput, false, true,
                    "Durable raw output may continue through deterministic validation without another dispatch."),
            RecoveryBoundaryClassification.AcceptedUnknown or RecoveryBoundaryClassification.ProviderUnknown or
                RecoveryBoundaryClassification.InFlight =>
                    Decision(TransitionRecoveryDisposition.ReconcileProvider, false, false,
                        "Provider work must be reconciled before any resubmission."),
            RecoveryBoundaryClassification.NotStarted =>
                Decision(TransitionRecoveryDisposition.SafeRetry, true, true,
                    "Durable evidence proves outward submission did not occur."),
            RecoveryBoundaryClassification.Cancelled =>
                Decision(TransitionRecoveryDisposition.Cancelled, false, false,
                    "Cancellation evidence and its salvage boundary are durable."),
            _ => Decision(TransitionRecoveryDisposition.NonRecoverableCorruption, false, false,
                "Recovery evidence is incomplete, corrupt, or explicitly failed; fail closed."),
        };

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
    public async Task<PromptExecutionResult> DispatchAsync(
        AuthorizedPromptDispatch request,
        CancellationToken cancellationToken)
    {
        var progress = new DurableProgress(request, _journal);
        progress.Observe(TransitionBoundaryKind.PreSubmission);
        using IDisposable scope = AgentTurnProgress.Use(progress);
        PromptExecutionResult result = await _inner.DispatchAsync(request, cancellationToken);
        await _evidence.RecordRawOutputAsync(
            request.Authorization.Causality,
            request.Authorization.Transition,
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
        AuthorizedPromptDispatch request,
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
                request.Authorization.Causality,
                request.Authorization.Transition,
                boundary,
                Interlocked.Increment(ref sequence),
                DateTimeOffset.UtcNow,
                request.Authorization.InputSnapshotHash,
                providerTurnId,
                [$"prompt:{request.Prompt.Value}"]);
            journal.RecordAsync(observation, CancellationToken.None).GetAwaiter().GetResult();
            if (journal.ShouldInterrupt(observation))
            {
                throw new TransitionFaultInjectedException(observation);
            }
        }
    }
}

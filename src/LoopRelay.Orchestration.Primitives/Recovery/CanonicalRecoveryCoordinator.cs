using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;

namespace LoopRelay.Orchestration.Recovery;

public static class TransitionRecoveryFactProjector
{
    public static RecoveryDurableFacts Project(TransitionRunRecoverySnapshot snapshot)
    {
        bool outwardStarted = snapshot.Boundaries.Any(item => item.Boundary is
            TransitionBoundaryKind.RequestWriteStarted or TransitionBoundaryKind.RequestSubmitted or
            TransitionBoundaryKind.RequestAccepted or TransitionBoundaryKind.ProviderTurnIdentified or
            TransitionBoundaryKind.PartialOutput or TransitionBoundaryKind.ProviderTerminal or
            TransitionBoundaryKind.ProviderCompleted);
        bool partialEffects = snapshot.State == TransitionDurableState.EffectsPartiallyApplied ||
            snapshot.Boundaries.Any(item => item.Boundary == TransitionBoundaryKind.DuringEffects);
        bool accepted = snapshot.Boundaries.Any(item => item.Boundary is
            TransitionBoundaryKind.RequestAccepted or TransitionBoundaryKind.ProviderTurnIdentified or
            TransitionBoundaryKind.PartialOutput or TransitionBoundaryKind.ProviderTerminal or
            TransitionBoundaryKind.ProviderCompleted);
        bool terminal = snapshot.Boundaries.Any(item => item.Boundary is
            TransitionBoundaryKind.ProviderTerminal or TransitionBoundaryKind.ProviderCompleted or
            TransitionBoundaryKind.RawOutputPersisted);
        int succeededEffects = snapshot.Effects.Count(item => item.Status == EffectExecutionStatus.Succeeded);
        var subject = new RecoveryCausalSubject(snapshot.Causality);
        return new RecoveryDurableFacts(
            RecoveryScopeKind.ProviderDispatch,
            subject,
            EvidenceComplete: snapshot.Boundaries.Count > 0 && !string.IsNullOrWhiteSpace(snapshot.InputSnapshotHash),
            Corrupt: false,
            Authorized: snapshot.State != TransitionDurableState.NotStarted,
            ValidInFlightCorrelation: snapshot.State == TransitionDurableState.Started && !outwardStarted,
            OutwardStarted: outwardStarted,
            OutwardAccepted: accepted,
            ProviderOutcomeUnknown: snapshot.State == TransitionDurableState.ProviderOutcomeUnknown,
            TerminalProviderResult: terminal,
            RawOutputDurable: snapshot.RawOutput is not null || snapshot.State is
                TransitionDurableState.PromptCompleted or TransitionDurableState.OutputInterpreted or
                TransitionDurableState.OutputValidated,
            OutputPromoted: snapshot.State == TransitionDurableState.Completed,
            ExplicitFailure: snapshot.State is TransitionDurableState.Failed or TransitionDurableState.Stalled,
            ExplicitCancellation: snapshot.State == TransitionDurableState.Cancelled,
            CancellationBoundary: snapshot.State != TransitionDurableState.Cancelled
                ? RecoveryCancellationBoundary.None
                : outwardStarted
                    ? RecoveryCancellationBoundary.AfterOutwardAcceptance
                    : RecoveryCancellationBoundary.BeforeDispatch,
            RequiredEffects: partialEffects ? Math.Max(2, snapshot.Effects.Count) : snapshot.Effects.Count,
            SucceededEffects: partialEffects ? Math.Max(1, succeededEffects) : succeededEffects,
            CompletionClosureStarted: false,
            CompletionClosureSettled: false,
            Evidence: snapshot.Evidence.Concat(snapshot.Boundaries.Select(item => $"boundary:{item.Boundary}"))
                .Distinct(StringComparer.Ordinal).ToArray());
    }
}

public static class CanonicalRecoveryClassifier
{
    public static CanonicalRecoveryClassification Classify(
        CanonicalRecoveryCase recoveryCase,
        RecoveryDurableFacts facts,
        RecoveryClassificationIdentity? supersedes = null,
        DateTimeOffset? observedAt = null)
    {
        if (recoveryCase.Scope != facts.Scope || recoveryCase.Subject != facts.Subject)
            throw new InvalidOperationException("Recovery facts do not describe the requested case subject.");

        RecoveryBoundaryClassification classification = facts switch
        {
            { Corrupt: true } => RecoveryBoundaryClassification.Corrupt,
            { EvidenceComplete: false } => RecoveryBoundaryClassification.EvidenceIncomplete,
            { CompletionClosureStarted: true, CompletionClosureSettled: false } =>
                RecoveryBoundaryClassification.CompletionPartiallyClosed,
            { RequiredEffects: > 0 } when facts.SucceededEffects > 0 &&
                facts.SucceededEffects < facts.RequiredEffects => RecoveryBoundaryClassification.PartiallyEffected,
            { ExplicitCancellation: true } => RecoveryBoundaryClassification.Cancelled,
            { ExplicitFailure: true } => RecoveryBoundaryClassification.Failed,
            { RawOutputDurable: true, OutputPromoted: false } => RecoveryBoundaryClassification.SucceededUncommitted,
            { ProviderOutcomeUnknown: true } => RecoveryBoundaryClassification.ProviderUnknown,
            { OutwardAccepted: true, TerminalProviderResult: false } => RecoveryBoundaryClassification.AcceptedUnknown,
            { OutwardStarted: true, TerminalProviderResult: false } => RecoveryBoundaryClassification.AcceptedUnknown,
            { Authorized: true, ValidInFlightCorrelation: true } => RecoveryBoundaryClassification.InFlight,
            { Authorized: false, OutwardStarted: false } => RecoveryBoundaryClassification.NotStarted,
            _ => RecoveryBoundaryClassification.EvidenceIncomplete,
        };

        return new CanonicalRecoveryClassification(
            RecoveryClassificationIdentity.New(),
            recoveryCase.Identity,
            classification,
            facts.ExplicitCancellation ? facts.CancellationBoundary : RecoveryCancellationBoundary.None,
            facts.Evidence.Distinct(StringComparer.Ordinal).ToArray(),
            supersedes,
            observedAt ?? DateTimeOffset.UtcNow);
    }
}

public static class CanonicalRecoveryPlanner
{
    public static CanonicalRecoveryPlan Plan(
        CanonicalRecoveryCase recoveryCase,
        CanonicalRecoveryClassification classification,
        RecoveryPlanningAuthority authority,
        DateTimeOffset? plannedAt = null)
    {
        if (classification.Case != recoveryCase.Identity)
            throw new InvalidOperationException("Recovery classification does not belong to the requested case.");

        CanonicalRecoveryAction selected = Select(recoveryCase, classification, authority);
        if (!authority.AllowedActions.Contains(selected))
            selected = CanonicalRecoveryAction.RequestHumanDecision;
        if (!authority.AllowedActions.Contains(selected))
            throw new InvalidOperationException("Resolved recovery policy permits no fail-closed recovery action.");

        bool exactProfileAction = selected is CanonicalRecoveryAction.ResumeSession or CanonicalRecoveryAction.NativeFork;
        if (exactProfileAction && !authority.ExactProfileSupported)
            throw new InvalidOperationException("Exact provider profile evidence does not authorize the selected recovery action.");

        string evidenceDigest = Hash(string.Join("\n", classification.SourceEvidence.Order(StringComparer.Ordinal)));
        string idempotencyKey =
            $"recovery-plan:{recoveryCase.Identity.Value}:{classification.Identity.Value}:{selected}:{evidenceDigest}";
        AttemptIdentity? nextAttempt = selected == CanonicalRecoveryAction.RetryNewAttempt
            ? AttemptIdentity.New()
            : null;
        return new CanonicalRecoveryPlan(
            RecoveryPlanIdentity.New(),
            recoveryCase.Identity,
            classification.Identity,
            selected,
            authority.ResolvedPolicyIdentity,
            authority.ExactProfileIdentity,
            classification.SourceEvidence.Concat(authority.Evidence).Distinct(StringComparer.Ordinal).ToArray(),
            Preconditions(selected, authority),
            Postconditions(selected),
            idempotencyKey,
            nextAttempt,
            plannedAt ?? DateTimeOffset.UtcNow);
    }

    private static CanonicalRecoveryAction Select(
        CanonicalRecoveryCase recoveryCase,
        CanonicalRecoveryClassification classification,
        RecoveryPlanningAuthority authority) => classification.Classification switch
    {
        RecoveryBoundaryClassification.NotStarted => authority.RetryAllowed
            ? CanonicalRecoveryAction.RetryNewAttempt
            : CanonicalRecoveryAction.Wait,
        RecoveryBoundaryClassification.InFlight => CanonicalRecoveryAction.Wait,
        RecoveryBoundaryClassification.AcceptedUnknown or RecoveryBoundaryClassification.ProviderUnknown =>
            CanonicalRecoveryAction.ReconcileProvider,
        RecoveryBoundaryClassification.SucceededUncommitted => CanonicalRecoveryAction.ReuseRawOutput,
        RecoveryBoundaryClassification.PartiallyEffected or RecoveryBoundaryClassification.CompletionPartiallyClosed =>
            CanonicalRecoveryAction.ReconcileEffects,
        RecoveryBoundaryClassification.Cancelled => Cancelled(classification, authority),
        RecoveryBoundaryClassification.Failed when
            recoveryCase.Scope is RecoveryScopeKind.WarmSession or RecoveryScopeKind.DecisionTurn =>
                SelectSessionRecovery(authority),
        RecoveryBoundaryClassification.Failed => authority.RetryAllowed
            ? CanonicalRecoveryAction.RetryNewAttempt
            : CanonicalRecoveryAction.RequestHumanDecision,
        RecoveryBoundaryClassification.Corrupt or RecoveryBoundaryClassification.EvidenceIncomplete =>
            CanonicalRecoveryAction.RequestHumanDecision,
        _ => CanonicalRecoveryAction.RequestHumanDecision,
    };

    private static CanonicalRecoveryAction Cancelled(
        CanonicalRecoveryClassification classification,
        RecoveryPlanningAuthority authority) => classification.CancellationBoundary switch
    {
        RecoveryCancellationBoundary.BeforeDispatch => authority.RetryAllowed
            ? CanonicalRecoveryAction.RetryNewAttempt
            : CanonicalRecoveryAction.Wait,
        RecoveryCancellationBoundary.AfterOutwardAcceptance => CanonicalRecoveryAction.ReconcileProvider,
        RecoveryCancellationBoundary.AfterValidatedOutput => CanonicalRecoveryAction.ReuseRawOutput,
        RecoveryCancellationBoundary.DuringEffects or RecoveryCancellationBoundary.DuringCompletionClosure =>
            CanonicalRecoveryAction.ReconcileEffects,
        _ => CanonicalRecoveryAction.RequestHumanDecision,
    };

    private static CanonicalRecoveryAction SelectSessionRecovery(RecoveryPlanningAuthority authority)
    {
        if (authority.ExactProfileSupported && authority.AllowedActions.Contains(CanonicalRecoveryAction.ResumeSession))
            return CanonicalRecoveryAction.ResumeSession;
        if (authority.ExactProfileSupported && authority.AllowedActions.Contains(CanonicalRecoveryAction.NativeFork))
            return CanonicalRecoveryAction.NativeFork;
        return authority.CertifiedReconstructionAvailable
            ? CanonicalRecoveryAction.ReconstructContext
            : CanonicalRecoveryAction.RequestHumanDecision;
    }

    private static string[] Preconditions(
        CanonicalRecoveryAction action,
        RecoveryPlanningAuthority authority) => action switch
    {
        CanonicalRecoveryAction.ReconcileProvider => ["provider correlation is durable", "no second dispatch"],
        CanonicalRecoveryAction.ReconcileEffects => ["original effect or closure plan is durable", "no duplicate mutation"],
        CanonicalRecoveryAction.ResumeSession or CanonicalRecoveryAction.NativeFork =>
            [$"exact profile {authority.ExactProfileIdentity} is certified"],
        CanonicalRecoveryAction.ReconstructContext => ["consumed inputs and rendered prompt facts are hash-bound"],
        CanonicalRecoveryAction.ReuseRawOutput => ["raw output is durable", "input freshness must be revalidated"],
        CanonicalRecoveryAction.RetryNewAttempt => ["source attempt remains immutable", "new attempt identity is reserved"],
        CanonicalRecoveryAction.Compensate => ["compensation is represented as an effect plan"],
        CanonicalRecoveryAction.Wait => ["no outward work is authorized"],
        _ => ["durable human response is required"],
    };

    private static string[] Postconditions(CanonicalRecoveryAction action) => action switch
    {
        CanonicalRecoveryAction.ReconcileProvider => ["provider outcome is durably classified"],
        CanonicalRecoveryAction.ReconcileEffects => ["required postconditions have receipts or remain explicitly pending"],
        CanonicalRecoveryAction.ResumeSession or CanonicalRecoveryAction.NativeFork or
            CanonicalRecoveryAction.ReconstructContext => ["active lineage is durably correlated"],
        CanonicalRecoveryAction.ReuseRawOutput => ["output is promoted without another dispatch"],
        CanonicalRecoveryAction.RetryNewAttempt => ["new attempt is linked to the unchanged transition run"],
        CanonicalRecoveryAction.Compensate => ["compensation receipts are verified"],
        CanonicalRecoveryAction.Wait => ["case remains nonterminal without mutation"],
        _ => ["human action request is durable"],
    };

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class CanonicalRecoveryCoordinator(ICanonicalRecoveryStore _store)
    : IRecoveryInspectUseCase, IRecoveryPlanUseCase
{
    public async Task<CanonicalRecoveryClassification> OpenCaseAsync(
        CanonicalRecoveryCase recoveryCase,
        RecoveryDurableFacts facts,
        CancellationToken cancellationToken = default)
    {
        CanonicalRecoveryClassification classification =
            CanonicalRecoveryClassifier.Classify(recoveryCase, facts);
        await _store.AppendCaseAndClassificationAsync(recoveryCase, classification, cancellationToken);
        return classification;
    }

    public async Task<CanonicalRecoveryClassification> ReclassifyAsync(
        RecoveryCaseIdentity recoveryCaseIdentity,
        RecoveryDurableFacts facts,
        CancellationToken cancellationToken = default)
    {
        CanonicalRecoveryCase recoveryCase = await _store.ReadCaseAsync(recoveryCaseIdentity, cancellationToken)
            ?? throw new KeyNotFoundException("Recovery case was not found.");
        CanonicalRecoveryClassification? previous =
            await _store.ReadLatestClassificationAsync(recoveryCaseIdentity, cancellationToken);
        CanonicalRecoveryClassification classification = CanonicalRecoveryClassifier.Classify(
            recoveryCase,
            facts,
            previous?.Identity);
        await _store.AppendClassificationAsync(classification, cancellationToken);
        return classification;
    }

    public async Task<(CanonicalRecoveryCase Case, CanonicalRecoveryClassification Classification)> InspectAsync(
        RecoveryInspectRequest request,
        CancellationToken cancellationToken = default)
    {
        CanonicalRecoveryCase recoveryCase = await _store.ReadCaseAsync(request.Case, cancellationToken)
            ?? throw new KeyNotFoundException("Recovery case was not found.");
        CanonicalRecoveryClassification classification =
            await _store.ReadLatestClassificationAsync(request.Case, cancellationToken)
            ?? throw new InvalidOperationException("Recovery case has no durable classification.");
        return (recoveryCase, classification);
    }

    public async Task<CanonicalRecoveryPlan> PlanAsync(
        RecoveryPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        (CanonicalRecoveryCase recoveryCase, CanonicalRecoveryClassification classification) =
            await InspectAsync(new RecoveryInspectRequest(request.Case), cancellationToken);
        CanonicalRecoveryPlan candidate = CanonicalRecoveryPlanner.Plan(
            recoveryCase,
            classification,
            request.Authority);
        CanonicalRecoveryPlan? existing = await _store.ReadPlanByIdempotencyKeyAsync(
            candidate.IdempotencyKey,
            cancellationToken);
        if (existing is not null) return existing;
        await _store.AppendPlanAsync(candidate, cancellationToken);
        return candidate;
    }
}

public sealed class CanonicalRecoveryCaseRecorder(ICanonicalRecoveryStore _store)
    : ICanonicalRecoveryCaseRecorder
{
    public async Task<CanonicalRecoveryClassification> RecordAsync(
        RecoveryScopeKind scope,
        RecoveryCausalSubject subject,
        RecoveryDurableFacts facts,
        CancellationToken cancellationToken = default)
    {
        string suffix = scope switch
        {
            RecoveryScopeKind.EffectPlan => subject.EffectPlanIdentity ?? subject.Causality.TransitionRun.Value,
            RecoveryScopeKind.CompletionClosure => subject.CompletionPlanIdentity ?? subject.Causality.TransitionRun.Value,
            RecoveryScopeKind.WarmSession => subject.SessionIdentity ?? subject.Causality.Attempt.Value,
            RecoveryScopeKind.DecisionTurn => subject.TurnIdentity ?? subject.Causality.Attempt.Value,
            RecoveryScopeKind.StorageOperation => subject.StorageOperationIdentity ?? subject.Causality.TransitionRun.Value,
            _ => subject.Causality.Attempt.Value,
        };
        var identity = new RecoveryCaseIdentity($"recoverycase:{scope}:{suffix}");
        var coordinator = new CanonicalRecoveryCoordinator(_store);
        CanonicalRecoveryCase? existing = await _store.ReadCaseAsync(identity, cancellationToken);
        if (existing is null)
        {
            return await coordinator.OpenCaseAsync(
                new CanonicalRecoveryCase(identity, scope, subject, DateTimeOffset.UtcNow),
                facts,
                cancellationToken);
        }
        CanonicalRecoveryClassification? latest =
            await _store.ReadLatestClassificationAsync(identity, cancellationToken);
        CanonicalRecoveryClassification candidate = CanonicalRecoveryClassifier.Classify(existing, facts, latest?.Identity);
        if (latest is not null &&
            latest.Classification == candidate.Classification &&
            latest.CancellationBoundary == candidate.CancellationBoundary &&
            latest.SourceEvidence.SequenceEqual(candidate.SourceEvidence, StringComparer.Ordinal))
        {
            return latest;
        }
        return await coordinator.ReclassifyAsync(identity, facts, cancellationToken);
    }
}

public sealed class CanonicalRecoveryRuntime(
    ICanonicalRecoveryStore _store,
    IEnumerable<ICanonicalRecoveryActionExecutor> executors) : IRecoveryExecuteUseCase
{
    private readonly IReadOnlyDictionary<CanonicalRecoveryAction, ICanonicalRecoveryActionExecutor> _executors =
        executors.ToDictionary(executor => executor.Action);

    public async Task<CanonicalRecoveryActionEvent> ExecuteAsync(
        RecoveryExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        CanonicalRecoveryPlan plan = await _store.ReadPlanAsync(request.Plan, cancellationToken)
            ?? throw new KeyNotFoundException("Recovery action cannot execute without a persisted plan.");
        IReadOnlyList<CanonicalRecoveryActionEvent> prior =
            await _store.ReadActionEventsAsync(plan.Identity, cancellationToken);
        CanonicalRecoveryActionEvent? terminal = prior.LastOrDefault(item => item.Lifecycle is
            RecoveryActionLifecycle.Succeeded or RecoveryActionLifecycle.Failed or
            RecoveryActionLifecycle.Waiting or RecoveryActionLifecycle.HumanActionRequired);
        if (terminal is not null) return terminal;

        CanonicalRecoveryActionEvent? previous = prior.LastOrDefault();
        RecoveryActionIdentity actionIdentity = previous?.Identity ?? RecoveryActionIdentity.New();
        bool reconcile = previous?.Lifecycle is RecoveryActionLifecycle.Started or RecoveryActionLifecycle.Unknown;
        if (!reconcile)
        {
            await _store.AppendActionEventAsync(
                new CanonicalRecoveryActionEvent(
                    actionIdentity, plan.Identity, RecoveryActionLifecycle.Started,
                    "Persisted recovery action execution started.", plan.SourceEvidence, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        try
        {
            CanonicalRecoveryActionResult result = _executors.TryGetValue(plan.Action, out ICanonicalRecoveryActionExecutor? executor)
                ? reconcile
                    ? await executor.ReconcileAsync(plan, previous!, cancellationToken)
                    : await executor.ExecuteAsync(plan, cancellationToken)
                : new CanonicalRecoveryActionResult(
                    plan.Action == CanonicalRecoveryAction.Wait
                        ? RecoveryActionLifecycle.Waiting
                        : RecoveryActionLifecycle.HumanActionRequired,
                    $"No automatic executor is registered for {plan.Action}.",
                    plan.SourceEvidence);
            var completed = new CanonicalRecoveryActionEvent(
                actionIdentity, plan.Identity, result.Lifecycle, result.Explanation,
                result.Evidence, DateTimeOffset.UtcNow);
            await _store.AppendActionEventAsync(completed, CancellationToken.None);
            return completed;
        }
        catch (OperationCanceledException)
        {
            await _store.AppendActionEventAsync(
                new CanonicalRecoveryActionEvent(
                    actionIdentity, plan.Identity, RecoveryActionLifecycle.Waiting,
                    "Caller cancellation stopped new recovery work; durable action evidence is retained.",
                    plan.SourceEvidence, DateTimeOffset.UtcNow),
                CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            var unknown = new CanonicalRecoveryActionEvent(
                actionIdentity, plan.Identity, RecoveryActionLifecycle.Unknown,
                $"Recovery action outcome requires reconciliation: {exception.Message}",
                plan.SourceEvidence, DateTimeOffset.UtcNow);
            await _store.AppendActionEventAsync(unknown, CancellationToken.None);
            return unknown;
        }
    }
}

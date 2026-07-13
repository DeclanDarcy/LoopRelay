using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;

namespace LoopRelay.Orchestration.Recovery;

public enum RecoveryScopeKind
{
    Transition,
    ProviderDispatch,
    EffectPlan,
    WarmSession,
    DecisionTurn,
    CompletionClosure,
    StorageOperation,
}

public enum RecoveryBoundaryClassification
{
    NotStarted,
    InFlight,
    AcceptedUnknown,
    SucceededUncommitted,
    Failed,
    Cancelled,
    ProviderUnknown,
    PartiallyEffected,
    CompletionPartiallyClosed,
    Corrupt,
    EvidenceIncomplete,
}

public enum CanonicalRecoveryAction
{
    ReconcileProvider,
    ReconcileEffects,
    ResumeSession,
    ReconstructContext,
    NativeFork,
    ReuseRawOutput,
    RetryNewAttempt,
    Compensate,
    Wait,
    RequestHumanDecision,
}

public enum RecoveryCancellationBoundary
{
    None,
    BeforeDispatch,
    AfterOutwardAcceptance,
    AfterValidatedOutput,
    DuringEffects,
    DuringCompletionClosure,
}

public enum RecoveryActionLifecycle
{
    Planned,
    Started,
    Succeeded,
    Failed,
    Unknown,
    Waiting,
    HumanActionRequired,
}

public sealed record RecoveryCausalSubject(
    CanonicalCausalContext Causality,
    string? SessionIdentity = null,
    string? TurnIdentity = null,
    string? EffectPlanIdentity = null,
    string? CompletionPlanIdentity = null,
    string? StorageOperationIdentity = null);

public sealed record RecoveryDurableFacts(
    RecoveryScopeKind Scope,
    RecoveryCausalSubject Subject,
    bool EvidenceComplete,
    bool Corrupt,
    bool Authorized,
    bool ValidInFlightCorrelation,
    bool OutwardStarted,
    bool OutwardAccepted,
    bool ProviderOutcomeUnknown,
    bool TerminalProviderResult,
    bool RawOutputDurable,
    bool OutputPromoted,
    bool ExplicitFailure,
    bool ExplicitCancellation,
    RecoveryCancellationBoundary CancellationBoundary,
    int RequiredEffects,
    int SucceededEffects,
    bool CompletionClosureStarted,
    bool CompletionClosureSettled,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalRecoveryCase(
    RecoveryCaseIdentity Identity,
    RecoveryScopeKind Scope,
    RecoveryCausalSubject Subject,
    DateTimeOffset CreatedAt);

public sealed record CanonicalRecoveryClassification(
    RecoveryClassificationIdentity Identity,
    RecoveryCaseIdentity Case,
    RecoveryBoundaryClassification Classification,
    RecoveryCancellationBoundary CancellationBoundary,
    IReadOnlyList<string> SourceEvidence,
    RecoveryClassificationIdentity? Supersedes,
    DateTimeOffset ObservedAt);

public sealed record RecoveryPlanningAuthority(
    string ResolvedPolicyIdentity,
    string ExactProfileIdentity,
    bool ExactProfileSupported,
    bool CertifiedReconstructionAvailable,
    bool RetryAllowed,
    IReadOnlySet<CanonicalRecoveryAction> AllowedActions,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalRecoveryPlan(
    RecoveryPlanIdentity Identity,
    RecoveryCaseIdentity Case,
    RecoveryClassificationIdentity Classification,
    CanonicalRecoveryAction Action,
    string ResolvedPolicyIdentity,
    string ExactProfileIdentity,
    IReadOnlyList<string> SourceEvidence,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Postconditions,
    string IdempotencyKey,
    AttemptIdentity? NewAttempt,
    DateTimeOffset PlannedAt);

public sealed record CanonicalRecoveryActionEvent(
    RecoveryActionIdentity Identity,
    RecoveryPlanIdentity Plan,
    RecoveryActionLifecycle Lifecycle,
    string Explanation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record CanonicalRecoveryActionResult(
    RecoveryActionLifecycle Lifecycle,
    string Explanation,
    IReadOnlyList<string> Evidence);

public interface ICanonicalRecoveryStore
{
    Task<CanonicalRecoveryCase?> ReadCaseAsync(RecoveryCaseIdentity identity, CancellationToken cancellationToken);
    Task<CanonicalRecoveryClassification?> ReadLatestClassificationAsync(RecoveryCaseIdentity identity, CancellationToken cancellationToken);
    Task<CanonicalRecoveryPlan?> ReadPlanAsync(RecoveryPlanIdentity identity, CancellationToken cancellationToken);
    Task<IReadOnlyList<CanonicalRecoveryPlan>> ReadPlansAsync(RecoveryCaseIdentity recoveryCase, CancellationToken cancellationToken);
    Task<CanonicalRecoveryPlan?> ReadPlanByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<CanonicalRecoveryActionEvent>> ReadActionEventsAsync(RecoveryPlanIdentity plan, CancellationToken cancellationToken);
    Task AppendCaseAndClassificationAsync(CanonicalRecoveryCase recoveryCase, CanonicalRecoveryClassification classification, CancellationToken cancellationToken);
    Task AppendClassificationAsync(CanonicalRecoveryClassification classification, CancellationToken cancellationToken);
    Task AppendPlanAsync(CanonicalRecoveryPlan plan, CancellationToken cancellationToken);
    Task AppendActionEventAsync(CanonicalRecoveryActionEvent actionEvent, CancellationToken cancellationToken);
}

public interface ICanonicalRecoveryActionExecutor
{
    CanonicalRecoveryAction Action { get; }
    Task<CanonicalRecoveryActionResult> ExecuteAsync(CanonicalRecoveryPlan plan, CancellationToken cancellationToken);
    Task<CanonicalRecoveryActionResult> ReconcileAsync(
        CanonicalRecoveryPlan plan,
        CanonicalRecoveryActionEvent previous,
        CancellationToken cancellationToken) => Task.FromResult(new CanonicalRecoveryActionResult(
            RecoveryActionLifecycle.HumanActionRequired,
            "This recovery action has no independent reconciliation implementation.",
            previous.Evidence));
}

public interface ICanonicalRecoveryCaseRecorder
{
    Task<CanonicalRecoveryClassification> RecordAsync(
        RecoveryScopeKind scope,
        RecoveryCausalSubject subject,
        RecoveryDurableFacts facts,
        CancellationToken cancellationToken = default);
}

public sealed record RecoveryInspectRequest(RecoveryCaseIdentity Case);
public sealed record RecoveryPlanRequest(RecoveryCaseIdentity Case, RecoveryPlanningAuthority Authority);
public sealed record RecoveryExecuteRequest(RecoveryPlanIdentity Plan);

public interface IRecoveryInspectUseCase
{
    Task<(CanonicalRecoveryCase Case, CanonicalRecoveryClassification Classification)> InspectAsync(
        RecoveryInspectRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryPlanUseCase
{
    Task<CanonicalRecoveryPlan> PlanAsync(
        RecoveryPlanRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryExecuteUseCase
{
    Task<CanonicalRecoveryActionEvent> ExecuteAsync(
        RecoveryExecuteRequest request,
        CancellationToken cancellationToken = default);
}

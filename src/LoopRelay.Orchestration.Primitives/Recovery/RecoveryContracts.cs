using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Orchestration.Recovery;

public enum RecoveryAttemptStatus
{
    Pending,
    ProtocolRepairRequired,
    ResumeSucceeded,
    RecoveryPreparing,
    ReplacementCreating,
    ReplacementCreated,
    ContextInjectionPending,
    RecoveryCompleted,
    RecoveryFailed,
    UnknownOutcome,
}

public enum RecoveryCompleteness
{
    Unknown,
    Full,
    Selective,
    Summary,
    RepositoryOnly,
}

public enum RecoveryActivationStrategy
{
    ReuseOriginal,
    EagerCreateAndInject,
    NativeClone,
}

public enum ActiveStateReadStatus
{
    Present,
    Absent,
    Corrupt,
    Conflict,
}

public sealed record RecoveryMechanismKey(string Identity, string Version);

public sealed record RecoverySourceDescriptor(
    int Order,
    string Kind,
    string Location,
    string Digest,
    string? VerifiedBoundary,
    string NormalizerVersion,
    RecoveryCompleteness Completeness,
    IReadOnlyList<string> Omissions,
    IReadOnlyDictionary<string, string> Evidence);

public sealed class RecoveryPlan
{
    public RecoveryPlan(
        string planId,
        string schemaVersion,
        string plannerVersion,
        string policyVersion,
        RecoveryMechanismKey mechanism,
        IReadOnlyList<string> eligibilityAndRankingEvidence,
        IReadOnlyList<RecoverySourceDescriptor> sources,
        string? envelopeDigest,
        IReadOnlyDictionary<string, string>? envelopeDescriptor,
        RecoveryActivationStrategy activationStrategy,
        string validationStrategy,
        string reconciliationStrategy,
        RecoveryCompleteness expectedCompleteness,
        IReadOnlyList<string> allowedOmissions,
        string continuityProfileDigest,
        IReadOnlyDictionary<string, string> operationConstraints,
        string idempotencyIdentity,
        int retryCeiling,
        string failureBehavior)
    {
        if (retryCeiling < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryCeiling));
        }

        PlanId = Required(planId, nameof(planId));
        SchemaVersion = Required(schemaVersion, nameof(schemaVersion));
        PlannerVersion = Required(plannerVersion, nameof(plannerVersion));
        PolicyVersion = Required(policyVersion, nameof(policyVersion));
        Mechanism = mechanism;
        EligibilityAndRankingEvidence = eligibilityAndRankingEvidence;
        Sources = sources.OrderBy(source => source.Order).ToArray();
        EnvelopeDigest = envelopeDigest;
        EnvelopeDescriptor = envelopeDescriptor ?? new Dictionary<string, string>();
        ActivationStrategy = activationStrategy;
        ValidationStrategy = Required(validationStrategy, nameof(validationStrategy));
        ReconciliationStrategy = Required(reconciliationStrategy, nameof(reconciliationStrategy));
        ExpectedCompleteness = expectedCompleteness;
        AllowedOmissions = allowedOmissions.Order(StringComparer.Ordinal).ToArray();
        ContinuityProfileDigest = Required(continuityProfileDigest, nameof(continuityProfileDigest));
        OperationConstraints = operationConstraints;
        IdempotencyIdentity = Required(idempotencyIdentity, nameof(idempotencyIdentity));
        RetryCeiling = retryCeiling;
        FailureBehavior = Required(failureBehavior, nameof(failureBehavior));
        Digest = RecoveryPlanSerializer.ComputeDigest(this);
    }

    public string PlanId { get; }
    public string SchemaVersion { get; }
    public string PlannerVersion { get; }
    public string PolicyVersion { get; }
    public RecoveryMechanismKey Mechanism { get; }
    public IReadOnlyList<string> EligibilityAndRankingEvidence { get; }
    public IReadOnlyList<RecoverySourceDescriptor> Sources { get; }
    public string? EnvelopeDigest { get; }
    public IReadOnlyDictionary<string, string> EnvelopeDescriptor { get; }
    public RecoveryActivationStrategy ActivationStrategy { get; }
    public string ValidationStrategy { get; }
    public string ReconciliationStrategy { get; }
    public RecoveryCompleteness ExpectedCompleteness { get; }
    public IReadOnlyList<string> AllowedOmissions { get; }
    public string ContinuityProfileDigest { get; }
    public IReadOnlyDictionary<string, string> OperationConstraints { get; }
    public string IdempotencyIdentity { get; }
    public int RetryCeiling { get; }
    public string FailureBehavior { get; }
    public string Digest { get; }

    private static string Required(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameter) : value;
}

public sealed record RecoveryFailure(
    string Classification,
    string ProviderMethod,
    int? StructuredCode,
    string? ProfileDigest,
    string RedactedDiagnostic,
    bool TurnSubmitted);

public sealed record RecoveryAttempt(
    string AttemptId,
    string? PreviousAttemptId,
    string ScopeId,
    string OriginalLineageId,
    string? ReplacementLineageId,
    string? TransitionRunId,
    RecoveryAttemptStatus Status,
    long RowVersion,
    string ProfileDigest,
    string? PlanDigest,
    RecoveryFailure? Failure,
    string Trigger,
    RecoveryMechanismKey? Mechanism,
    string IdempotencyKey,
    string? ProviderRequestId,
    string? ProviderCorrelationId,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record RecoveryDomainEvent(
    string AttemptId,
    RecoveryAttemptStatus From,
    RecoveryAttemptStatus To,
    string EventName,
    DateTimeOffset RecordedAt);

public sealed record RecoveryJournalTransition(RecoveryAttempt Attempt, IReadOnlyList<RecoveryDomainEvent> Events);

public sealed record DecisionSessionScopeRecord(
    string ScopeId,
    string WorkspaceId,
    string PreparedEpicCausalId,
    string ExecutablePlanCausalId,
    string Role,
    string ContractVersion,
    string LifecycleState,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RetiredAt);

public sealed record DecisionSessionLineageNode(
    string LineageId,
    string? ScopeId,
    string Provider,
    string ProviderSessionId,
    string? ParentLineageId,
    string RootLineageId,
    string Mechanism,
    RecoveryCompleteness Completeness,
    string? SourceDigest,
    string? ProfileDigest,
    string? PlanDigest,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RetiredAt,
    string AuthorityState);

public sealed record DecisionSessionAccounting(
    int OccupancyTokens,
    double ReuseCost,
    int ReuseCycles,
    double LastCycleCost,
    double PreviousCycleCost,
    double TransferCost,
    int TransferCount,
    int? PreviousContextSize,
    int ContextGrowthStreak);

public sealed record DecisionSessionActiveState(
    string ScopeId,
    string LineageId,
    DecisionSessionAccounting Accounting,
    string PolicyDigest,
    string? ProjectionDigest,
    long RowVersion,
    DateTimeOffset ActivatedAt);

public sealed record ActiveStateReadResult(
    ActiveStateReadStatus Status,
    DecisionSessionActiveState? Active,
    DecisionSessionLineageNode? Lineage,
    string? Diagnostic);

public sealed record RecoveryStoreWriteResult(bool Succeeded, bool Conflict, long? RowVersion, string? Diagnostic);

public enum DecisionTurnState
{
    Pending,
    WriteStarted,
    Submitted,
    Accepted,
    Terminal,
    Unknown,
    Committed,
    Materialized,
}

public sealed record DecisionSessionTurnRecord(
    string TurnRecordId,
    string ScopeId,
    string LineageId,
    string TransitionRunId,
    string InputSnapshotHash,
    string ProviderThreadId,
    string? ProviderTurnId,
    string? RequestId,
    DecisionTurnState State,
    bool WriteStarted,
    bool Submitted,
    bool Accepted,
    bool Terminal,
    string? OutputBody,
    string? OutputHash,
    string? HistoryKind,
    int? HistorySequence,
    bool ArtifactMaterialized,
    string? ReconciliationJson,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DecisionTurnCommitResult(
    RecoveryStoreWriteResult Write,
    DecisionSessionTurnRecord? Turn,
    DecisionSessionActiveState? Active,
    string? HistoryRelativePath);

public sealed record DecisionContinuityStatusSnapshot(
    int ActiveScopeCount,
    DecisionSessionActiveState? Active,
    DecisionSessionLineageNode? Lineage,
    IReadOnlyList<DecisionSessionLineageNode> Ancestry,
    RecoveryAttempt? LatestAttempt,
    RecoveryAttempt? UnresolvedAttempt,
    DecisionSessionTurnRecord? UnresolvedTurn,
    string? Diagnostic);

public sealed record RecoveryPlanningInput(
    RecoveryFailure Failure,
    string ScopeId,
    SessionContinuityProfile Profile,
    IReadOnlyList<RecoverySourceDescriptor> Sources,
    IReadOnlyDictionary<string, string> Policy,
    int ContextBudget,
    string? EnvelopeDigest = null,
    IReadOnlyDictionary<string, string>? EnvelopeDescriptor = null);

public sealed record RecoveryEnvelopePayload(
    string Marker,
    string CanonicalContent,
    string Digest,
    IReadOnlyDictionary<string, string> Descriptor);

public interface IRecoveryEnvelopeFactory
{
    RecoveryEnvelopePayload Build(
        string attemptId,
        string scopeId,
        ProviderSessionReference original,
        RecoveryFailure failure,
        IReadOnlyList<RecoverySourceObservation> sources,
        SessionContinuityProfile profile,
        int contextBudget);
}

public sealed record RecoveryMechanismEligibility(bool Eligible, RecoveryCompleteness Completeness, IReadOnlyList<string> Evidence);

public enum RecoveryMechanismExecutionStatus
{
    Succeeded,
    KnownFailure,
    UnknownOutcome,
}

public enum RecoveryMechanismExecutionPhase
{
    CreateReplacement,
    InjectContext,
}

public sealed record RecoveryMechanismExecutionRequest(
    RecoveryMechanismExecutionPhase Phase,
    RecoveryPlan Plan,
    SessionCreateRequest CreateRequest,
    ProviderSessionReference Original,
    IReadOnlyList<RecoverySourceObservation> Sources,
    string? CanonicalEnvelope,
    string Marker,
    IAgentSessionContinuityRuntime ContinuityRuntime,
    IAgentSession? ExistingSession = null,
    ProviderSessionReference? ExistingReplacement = null);

public sealed record RecoveryMechanismExecutionResult(
    RecoveryMechanismExecutionStatus Status,
    IAgentSession? Session,
    ProviderSessionReference? Replacement,
    SessionCreateResult? Create,
    SessionSeedResult? Seed,
    SessionOperationFailure? Failure,
    string? Diagnostic,
    SessionForkResult? Fork = null);

public sealed record RecoveryMechanismValidationResult(
    bool Valid,
    bool Unknown,
    string? Diagnostic);

public interface IRecoveryPlanner
{
    RecoveryPlan Plan(RecoveryPlanningInput input, IReadOnlyList<IRecoveryMechanism> mechanisms);
}

public interface IRecoveryMechanism
{
    RecoveryMechanismKey Key { get; }
    string LineageMechanism { get; }
    bool RequiresContextInjection { get; }
    RecoveryActivationStrategy ActivationStrategy { get; }
    string ValidationStrategy { get; }
    string ReconciliationStrategy { get; }
    RecoveryRuntimeOutcome SuccessOutcome(RecoveryCompleteness completeness);
    RecoveryMechanismEligibility EvaluateEligibility(RecoveryPlanningInput input);
    Task<RecoveryMechanismExecutionResult> ExecuteAsync(
        RecoveryMechanismExecutionRequest request,
        CancellationToken cancellationToken = default);
    Task<RecoveryMechanismExecutionResult> ReconcileAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult previous,
        CancellationToken cancellationToken = default);
    Task<RecoveryMechanismValidationResult> ValidateAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult result,
        CancellationToken cancellationToken = default);
}

public interface IRecoveryMechanismCatalog
{
    IReadOnlyList<IRecoveryMechanism> All { get; }
    IRecoveryMechanism Resolve(RecoveryMechanismKey key);
}

public interface IRecoverySource
{
    string Kind { get; }
    Task<RecoverySourceObservation?> ObserveAsync(
        RecoverySourceRequest request,
        CancellationToken cancellationToken);
}

public sealed record RecoverySourceRequest(
    string ScopeId,
    AgentSessionSpec SessionSpec,
    ProviderSessionReference Original,
    SessionContinuityProfile Profile);

public sealed record RecoverySourceObservation(
    RecoverySourceDescriptor Descriptor,
    IReadOnlyList<SessionContentRecord> Records);

public interface IRecoverySourceCatalog
{
    IReadOnlyList<IRecoverySource> All { get; }
}

public enum RecoveryRuntimeOutcome
{
    ResumedOriginal,
    ReplacementNativeFork,
    ReplacementRecoveredFull,
    ReplacementRecoveredPartial,
    ReplacementRepositoryOnly,
    ProtocolRepairRequired,
    FailedClosed,
    UnknownOutcome,
    NoActiveSession,
}

public sealed record RecoveryRuntimeRequest(
    string ScopeId,
    string? TransitionRunId,
    AgentSessionSpec ResumeSessionSpec,
    AgentSessionSpec FreshSessionSpec,
    SessionContinuityProfile Profile,
    IReadOnlyDictionary<string, string> Policy,
    int ContextBudget,
    string Trigger,
    CanonicalCausalContext? Causality = null);

public sealed record RecoveryRuntimeResult(
    RecoveryRuntimeOutcome Outcome,
    IAgentSession? Session,
    ProviderSessionReference? ProviderSession,
    RecoveryAttempt? Attempt,
    RecoveryPlan? Plan,
    RecoveryCompleteness Completeness,
    bool Seeded,
    string? Diagnostic);

public interface IRecoveryRuntime
{
    Task<RecoveryRuntimeResult> RunAsync(
        RecoveryRuntimeRequest request,
        CancellationToken cancellationToken = default);
}

using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Models;

public sealed record DecisionSessionLifecycleProjection(
    Guid RepositoryId,
    DecisionSessionProjection? ActiveSession,
    IReadOnlyList<DecisionSessionProjection> Sessions,
    DecisionSessionMetricsSnapshot? Metrics,
    DecisionSessionSizeProjection? Size,
    DecisionSessionEconomicsSnapshot? Economics,
    DecisionSessionCoherenceSnapshot? Coherence,
    DecisionSessionLifecycleSnapshot? Policy,
    DecisionSessionTransferEligibilitySnapshot? TransferEligibility,
    DecisionSessionContinuityArtifact? CurrentContinuityArtifact,
    IReadOnlyList<DecisionSessionContinuityArtifactProjection> ContinuityArtifacts,
    IReadOnlyList<DecisionSessionTransfer> RecentTransfers,
    IReadOnlyList<DecisionSessionTransferEvent> RecentTransferEvents,
    IReadOnlyList<DecisionSessionTransferEventProjection> TransferEvents,
    IReadOnlyList<DecisionSessionRecoveryResult> RecentRecoveryResults,
    DecisionSessionDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionSizeProjection(
    long EstimatedTokenCount,
    long ContextByteSize,
    long ReasoningEventCount,
    long DecisionCount,
    TimeSpan SessionAge,
    TimeSpan IdleDuration,
    decimal CacheMissRisk,
    DateTimeOffset MeasuredAt);

public sealed record DecisionSessionTransferEventProjection(
    string TransferId,
    DecisionSessionId SourceSessionId,
    DecisionSessionId? TargetSessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool Succeeded,
    string? Reason,
    long? EstimatedTokenCount,
    DecisionSessionLifecycleDecision? PolicyDecision,
    decimal? ReuseScore,
    decimal? TransferScore,
    DecisionSessionTransferEligibilityStatus? EligibilityStatus,
    string? ContinuityArtifactId,
    IReadOnlyList<DecisionSessionTransferEvent> Events,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionSessionContinuityArtifactProjection(
    string ArtifactId,
    string ContinuityFingerprint,
    DecisionSessionId SourceSessionId,
    DecisionSessionId? TargetSessionId,
    IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences,
    IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences,
    IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Diagnostics);

public enum DecisionSessionHealthStatus
{
    Healthy,
    Warning,
    Unhealthy,
    Unknown
}

public sealed record DecisionSessionHealthDimension(
    string Name,
    DecisionSessionHealthStatus Status,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Evidence);

public sealed record DecisionSessionHealthAssessment(
    Guid RepositoryId,
    IReadOnlyList<DecisionSessionHealthDimension> Dimensions,
    DecisionSessionInfluenceTrace InfluenceTrace,
    DateTimeOffset GeneratedAt);

public enum DecisionSessionLifecycleHistoryEventType
{
    Created,
    Activated,
    AnalysisCaptured,
    PolicyEvaluated,
    TransferEligibilityEvaluated,
    ContinuityArtifactCreated,
    TransferStarted,
    TransferCompleted,
    Retired,
    ReplacementCreated,
    Recovered
}

public sealed record DecisionSessionLifecycleHistoryEvent(
    DecisionSessionLifecycleHistoryEventType EventType,
    DateTimeOffset OccurredAt,
    DecisionSessionId? SessionId,
    DecisionSessionId? RelatedSessionId,
    string? ContinuityArtifactId,
    string? TransferId,
    string? RecoveryId,
    string Message,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionSessionLifecycleHistory(
    Guid RepositoryId,
    IReadOnlyList<DecisionSessionLifecycleHistoryEvent> Events,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionInfluenceSignal(
    string Category,
    string Name,
    decimal? Score,
    string Value,
    string Description,
    IReadOnlyList<string> ContributingFactors);

public sealed record DecisionSessionInfluenceTrace(
    Guid RepositoryId,
    DecisionSessionId? ActiveSessionId,
    DecisionSessionLifecycleDecision? PolicyDecision,
    DecisionSessionTransferEligibilityStatus? TransferEligibilityStatus,
    IReadOnlyList<DecisionSessionInfluenceSignal> Signals,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset GeneratedAt);

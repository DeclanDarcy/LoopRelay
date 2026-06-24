using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Models;

public sealed record DecisionSessionLifecycleProjection(
    Guid RepositoryId,
    DecisionSessionProjection? ActiveSession,
    IReadOnlyList<DecisionSessionProjection> Sessions,
    DecisionSessionMetricsSnapshot? Metrics,
    DecisionSessionEconomicsSnapshot? Economics,
    DecisionSessionCoherenceSnapshot? Coherence,
    DecisionSessionLifecycleSnapshot? Policy,
    DecisionSessionTransferEligibilitySnapshot? TransferEligibility,
    DecisionSessionContinuityArtifact? CurrentContinuityArtifact,
    IReadOnlyList<DecisionSessionTransfer> RecentTransfers,
    IReadOnlyList<DecisionSessionTransferEvent> RecentTransferEvents,
    IReadOnlyList<DecisionSessionRecoveryResult> RecentRecoveryResults,
    DecisionSessionDiagnostics Diagnostics,
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

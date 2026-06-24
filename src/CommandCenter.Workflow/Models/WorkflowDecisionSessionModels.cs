namespace CommandCenter.Workflow.Models;

public sealed record WorkflowDecisionSessionProjection(
    Guid RepositoryId,
    string? DecisionSessionId,
    string? DecisionSessionState,
    long? EstimatedTokenCount,
    TimeSpan? EstimatedCacheTtl,
    decimal? EstimatedCacheMissRisk,
    decimal? ReuseScore,
    decimal? TransferScore,
    decimal? CoherenceScore,
    decimal? TransferPressure,
    string? CurrentLifecycleDecision,
    string? TransferEligibilityStatus,
    string? ContinuityArtifactId,
    string? ContinuityFingerprint,
    IReadOnlyList<WorkflowTransferProjection> TransferLineage,
    IReadOnlyList<WorkflowContinuityArtifactProjection> ContinuityArtifactLineage,
    IReadOnlyList<WorkflowGovernanceHealthProjection> GovernanceHealthDimensions,
    WorkflowGovernanceSummary Summary,
    WorkflowGovernanceReadiness Readiness,
    DecisionSessionWorkflowDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record WorkflowGovernanceSummary(
    Guid RepositoryId,
    string? DecisionSessionId,
    string? DecisionSessionState,
    string? LifecycleDecision,
    string? TransferEligibilityStatus,
    long? EstimatedTokenCount,
    TimeSpan? EstimatedCacheTtl,
    decimal? EstimatedCacheMissRisk,
    decimal? CoherenceScore,
    decimal? TransferPressure,
    string HealthStatus,
    IReadOnlyList<string> Highlights,
    DateTimeOffset GeneratedAt);

public sealed record WorkflowTransferProjection(
    string TransferId,
    string SourceSessionId,
    string? TargetSessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool Succeeded,
    string? ContinuityArtifactId,
    string Status,
    IReadOnlyList<string> Diagnostics);

public sealed record WorkflowContinuityArtifactProjection(
    string ArtifactId,
    string ContinuityFingerprint,
    string SourceSessionId,
    string? TargetSessionId,
    DateTimeOffset CreatedAt,
    int DecisionReferenceCount,
    int ReasoningReferenceCount,
    int OperationalContextReferenceCount,
    IReadOnlyList<string> Diagnostics);

public sealed record WorkflowGovernanceHealthProjection(
    string Name,
    string Status,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Evidence);

public sealed record WorkflowGovernanceInfluenceProjection(
    Guid RepositoryId,
    string? DecisionSessionId,
    string? LifecycleDecision,
    string? TransferEligibilityStatus,
    IReadOnlyList<WorkflowGovernanceInfluenceSignal> Signals,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record WorkflowGovernanceInfluenceSignal(
    string Category,
    string Name,
    decimal? Score,
    string Value,
    string Description,
    IReadOnlyList<string> ContributingFactors);

public sealed record WorkflowGovernanceReadiness(
    bool HasActiveSession,
    bool HasAnalysis,
    bool HasPolicy,
    bool HasEligibility,
    bool IsTransferRecommended,
    bool IsTransferEligible,
    bool HasContinuityArtifact,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> BlockingSignals);

public sealed record DecisionSessionWorkflowDiagnostics(
    Guid RepositoryId,
    bool IsValid,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    DateTimeOffset GeneratedAt);

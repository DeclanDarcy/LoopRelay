using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Models;

public sealed record DecisionSessionEvidenceSource(
    string Source,
    long ItemCount,
    long ByteCount,
    long CharacterCount,
    string SerializedContent,
    DateTimeOffset? LastActivityAt,
    IReadOnlyList<string> Notes);

public sealed record DecisionSessionEvidence(
    Guid RepositoryId,
    DateTimeOffset SessionStartedAt,
    DateTimeOffset LastActivityAt,
    long EvidenceItemCount,
    long DecisionCount,
    long DecisionCandidateCount,
    long DecisionProposalCount,
    long ReasoningEventCount,
    long ReasoningThreadCount,
    long ReasoningRelationshipCount,
    long OperationalContextRevisionCount,
    IReadOnlyList<DecisionSessionEvidenceSource> Sources,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionMetrics(
    long EstimatedTokenCount,
    long ContextByteSize,
    long ReasoningEventCount,
    long ReasoningThreadCount,
    long ReasoningRelationshipCount,
    long DecisionCount,
    long DecisionCandidateCount,
    long DecisionProposalCount,
    long OperationalContextRevisionCount,
    DateTimeOffset LastActivityAt,
    DateTimeOffset MeasuredAt);

public sealed record DecisionSessionStatistics(
    TimeSpan SessionAge,
    TimeSpan SessionElapsedDuration,
    TimeSpan IdleDuration,
    decimal GrowthRate,
    decimal ActivityRate);

public sealed record DecisionSessionActivity(
    long EvidenceItemCount,
    DateTimeOffset LastActivityAt,
    TimeSpan IdleDuration,
    decimal ActivityRate);

public sealed record DecisionSessionGrowth(
    long EvidenceByteSize,
    long EstimatedTokenCount,
    TimeSpan ElapsedDuration,
    decimal GrowthRate);

public sealed record DecisionSessionCacheMetrics(
    TimeSpan EstimatedCacheTtl,
    decimal EstimatedCacheMissRisk,
    DateTimeOffset? EstimatedCacheExpiresAt);

public sealed record DecisionSessionMetricsSourceDiagnostic(
    string Source,
    long ItemCount,
    long ByteCount,
    long CharacterCount,
    IReadOnlyList<string> Notes);

public sealed record DecisionSessionMetricsDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DecisionSessionMetricsSourceDiagnostic> Sources,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionMetricsSnapshot(
    Guid RepositoryId,
    DecisionSessionMetrics Metrics,
    DecisionSessionStatistics Statistics,
    DecisionSessionActivity Activity,
    DecisionSessionGrowth Growth,
    DecisionSessionCacheMetrics Cache,
    DecisionSessionMetricsDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionEconomicsOptions(
    long LargeContextTokenThreshold = 200_000,
    long LargeContextByteThreshold = 800_000,
    long ReasoningEventThreshold = 500,
    long ReasoningThreadThreshold = 100,
    long ReasoningRelationshipThreshold = 300,
    long DecisionThreshold = 100,
    long OperationalContextRevisionThreshold = 50,
    decimal CachedTokenCostFactor = 0.10m,
    decimal AssumedCoherenceScore = 0.50m);

public sealed record DecisionSessionEconomicsInputs(
    DecisionSessionMetrics Metrics,
    DecisionSessionStatistics Statistics,
    DecisionSessionActivity Activity,
    DecisionSessionGrowth Growth,
    DecisionSessionCacheMetrics Cache);

public sealed record ReuseValueAssessment(
    decimal Value,
    decimal ContinuityContribution,
    decimal CacheContribution,
    decimal CoherenceContribution,
    decimal ActivityContribution);

public sealed record TransferValueAssessment(
    decimal Value,
    decimal GrowthContribution,
    decimal IdleContribution,
    decimal CacheRiskContribution,
    decimal ContextCostContribution);

public sealed record CacheBenefitAssessment(
    decimal Value,
    decimal ReusableCorpusScore,
    decimal CachedTokenCostFactor,
    decimal CacheRiskPenalty);

public sealed record CacheRiskAssessment(
    decimal Value,
    TimeSpan EstimatedCacheTtl,
    DateTimeOffset? EstimatedCacheExpiresAt);

public sealed record ContinuityBenefitAssessment(
    decimal Value,
    decimal DecisionContribution,
    decimal ReasoningDensityContribution,
    decimal OperationalContextContribution);

public sealed record DecisionSessionEconomics(
    decimal EstimatedReuseValue,
    decimal EstimatedTransferValue,
    decimal EstimatedContextCost,
    decimal EstimatedReasoningCost,
    decimal EstimatedContinuityBenefit,
    decimal EstimatedCacheBenefit,
    decimal EstimatedCacheMissRisk);

public sealed record DecisionSessionEconomicsDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionEconomicsInputs Inputs,
    ReuseValueAssessment ReuseValue,
    TransferValueAssessment TransferValue,
    CacheBenefitAssessment CacheBenefit,
    CacheRiskAssessment CacheRisk,
    ContinuityBenefitAssessment ContinuityBenefit,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionEconomicsSnapshot(
    Guid RepositoryId,
    DecisionSessionEconomics Economics,
    DecisionSessionEconomicsDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionCoherenceOptions(
    long DenseGraphRelationshipThreshold = 300,
    long GovernanceEvidenceThreshold = 150,
    long OperationalContextRevisionThreshold = 50,
    decimal HighGrowthRateThreshold = 800_000m);

public sealed record DecisionSessionCoherenceInputs(
    DecisionSessionMetrics Metrics,
    DecisionSessionStatistics Statistics,
    DecisionSessionCacheMetrics Cache,
    DecisionSessionEconomics Economics,
    long GraphNodeCount,
    long GraphRelationshipCount,
    long IsolatedNodeCount,
    long DisconnectedGroupCount,
    long ResolvedNodeCount,
    long UnresolvedNodeCount);

public sealed record FragmentationAssessment(
    decimal Score,
    decimal IsolatedNodeContribution,
    decimal DisconnectedGroupContribution,
    decimal LowDensityContribution);

public sealed record DensityAssessment(
    decimal Score,
    decimal RelationshipDensity,
    long NodeCount,
    long RelationshipCount);

public sealed record ContinuityQualityAssessment(
    decimal Score,
    decimal GovernanceEvidenceContribution,
    decimal CrossReferenceContribution,
    decimal OperationalContextContribution,
    decimal ResolvedReferenceContribution);

public sealed record TransferPressureAssessment(
    decimal Score,
    decimal FragmentationContribution,
    decimal GrowthContribution,
    decimal LowCoherenceContribution,
    decimal CacheRiskContribution,
    decimal ContextCostContribution);

public sealed record DecisionSessionCoherence(
    decimal CoherenceScore,
    decimal FragmentationScore,
    decimal DensityScore,
    decimal ContinuityScore,
    decimal TransferPressure);

public sealed record DecisionSessionCoherenceDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionCoherenceInputs Inputs,
    FragmentationAssessment Fragmentation,
    DensityAssessment Density,
    ContinuityQualityAssessment ContinuityQuality,
    TransferPressureAssessment TransferPressure,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionCoherenceSnapshot(
    Guid RepositoryId,
    DecisionSessionCoherence Coherence,
    DecisionSessionCoherenceDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionAnalysisDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionMetricsDiagnostics Metrics,
    DecisionSessionEconomicsDiagnostics Economics,
    DecisionSessionCoherenceDiagnostics Coherence,
    IReadOnlyList<string> Warnings);

public enum DecisionSessionLifecycleDecision
{
    Continue,
    Transfer
}

public sealed record DecisionSessionLifecyclePolicyOptions(
    decimal ReuseEconomicsWeight = 0.30m,
    decimal CacheBenefitWeight = 0.25m,
    decimal ContinuityBenefitWeight = 0.20m,
    decimal CoherenceWeight = 0.25m,
    decimal TransferEconomicsWeight = 0.25m,
    decimal TransferPressureWeight = 0.30m,
    decimal FragmentationWeight = 0.20m,
    decimal GrowthWeight = 0.10m,
    decimal CacheMissRiskWeight = 0.15m,
    decimal HighGrowthRateThreshold = 800_000m);

public sealed record DecisionSessionLifecycleInputs(
    DecisionSession Session,
    DecisionSessionMetrics Metrics,
    DecisionSessionStatistics Statistics,
    DecisionSessionCacheMetrics Cache,
    DecisionSessionEconomics Economics,
    DecisionSessionCoherence Coherence);

public sealed record ReuseScoreAssessment(
    decimal Score,
    decimal EstimatedReuseValueContribution,
    decimal CacheBenefitContribution,
    decimal ContinuityBenefitContribution,
    decimal CoherenceContribution);

public sealed record TransferScoreAssessment(
    decimal Score,
    decimal EstimatedTransferValueContribution,
    decimal TransferPressureContribution,
    decimal FragmentationContribution,
    decimal GrowthContribution,
    decimal CacheMissRiskContribution);

public sealed record DecisionSessionLifecycleEvaluation(
    DecisionSessionLifecycleDecision Decision,
    decimal ReuseScore,
    decimal TransferScore,
    string Reason,
    IReadOnlyList<string> ContributingFactors,
    DateTimeOffset EvaluatedAt);

public sealed record DecisionSessionLifecycleDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionLifecycleInputs Inputs,
    ReuseScoreAssessment ReuseScore,
    TransferScoreAssessment TransferScore,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionLifecycleSnapshot(
    Guid RepositoryId,
    DecisionSessionLifecycleEvaluation Evaluation,
    DecisionSessionLifecycleDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public enum DecisionSessionTransferEligibilityStatus
{
    NotApplicable,
    Eligible,
    Blocked,
    Deferred
}

public sealed record DecisionSessionTransferEligibilityFinding(
    string Code,
    string Severity,
    string Message);

public sealed record DecisionSessionTransferEligibility(
    DecisionSessionTransferEligibilityStatus Status,
    DecisionSessionLifecycleEvaluation PolicyEvaluation,
    DecisionSessionId? SourceSessionId,
    IReadOnlyList<DecisionSessionTransferEligibilityFinding> Findings,
    DateTimeOffset CheckedAt);

public sealed record DecisionSessionTransferEligibilityInputs(
    DecisionSessionLifecycleEvaluation PolicyEvaluation,
    DecisionSessionDiagnostics RegistryDiagnostics,
    DecisionSession? ActiveSession,
    DecisionSessionEvidence? Evidence);

public sealed record DecisionSessionTransferEligibilityDiagnostics(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionTransferEligibilityInputs Inputs,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings);

public sealed record DecisionSessionTransferEligibilitySnapshot(
    Guid RepositoryId,
    DecisionSessionTransferEligibility Eligibility,
    DecisionSessionTransferEligibilityDiagnostics Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionContinuityReference(
    string Source,
    string ReferenceType,
    long ItemCount,
    long ByteCount,
    DateTimeOffset? LastActivityAt,
    string Fingerprint);

public sealed record DecisionSessionContinuityArtifact(
    string ArtifactId,
    Guid RepositoryId,
    DecisionSessionId SourceSessionId,
    DecisionSessionId? TargetSessionId,
    DateTimeOffset CreatedAt,
    DecisionSessionLifecycleEvaluation PolicyEvaluation,
    DecisionSessionMetrics Metrics,
    DecisionSessionEconomics Economics,
    DecisionSessionCoherence Coherence,
    DecisionSessionCacheMetrics Cache,
    IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences,
    IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences,
    IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences,
    string ContinuityFingerprint,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionSessionContinuityArtifactValidation(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static DecisionSessionContinuityArtifactValidation Valid { get; } = new(true, [], []);
}

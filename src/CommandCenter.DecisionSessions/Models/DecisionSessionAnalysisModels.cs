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

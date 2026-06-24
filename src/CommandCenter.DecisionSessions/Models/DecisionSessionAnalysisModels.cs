namespace CommandCenter.DecisionSessions.Models;

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

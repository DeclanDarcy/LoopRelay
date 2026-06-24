namespace CommandCenter.Middle.Projections;

public sealed class RepositoryDecisionSessionSummary
{
    public string? DecisionSessionId { get; init; }

    public string? State { get; init; }

    public string? LifecycleDecision { get; init; }

    public string? TransferEligibilityStatus { get; init; }

    public long? EstimatedTokenCount { get; init; }

    public TimeSpan? EstimatedCacheTtl { get; init; }

    public decimal? CacheMissRisk { get; init; }

    public decimal? CoherenceScore { get; init; }

    public decimal? TransferPressure { get; init; }

    public IReadOnlyList<RepositoryDecisionSessionHealthDimension> HealthDimensions { get; init; } = [];

    public IReadOnlyList<RepositoryDecisionSessionTransferSummary> RecentTransferLineage { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public DateTimeOffset? GeneratedAt { get; init; }
}

public sealed class RepositoryDecisionSessionHealthDimension
{
    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public IReadOnlyList<string> Findings { get; init; } = [];
}

public sealed class RepositoryDecisionSessionTransferSummary
{
    public string TransferId { get; init; } = string.Empty;

    public string SourceSessionId { get; init; } = string.Empty;

    public string? TargetSessionId { get; init; }

    public string? ContinuityArtifactId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public bool Succeeded { get; init; }
}

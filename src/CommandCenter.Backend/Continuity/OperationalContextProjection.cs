namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextProjection
{
    public bool Exists { get; init; }

    public string? CurrentRelativePath { get; init; }

    public int RevisionCount { get; init; }

    public int CurrentRevisionNumber { get; init; }

    public DateTimeOffset? LastUpdatedAt { get; init; }

    public DateTimeOffset? LastPromotionAt { get; init; }

    public IReadOnlyList<string> CurrentUnderstandingSummary { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> Architecture { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> AuthorityBoundaries { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> Constraints { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> StableDecisions { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> DecisionRationale { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> OpenQuestions { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> ActiveRisks { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> RecentUnderstandingChanges { get; init; } = [];

    public OperationalContextProposalSummary PendingProposalSummary { get; init; } = new();

    public OperationalContextReviewState? LatestReviewState { get; init; }

    public IReadOnlyList<string> ContinuityWarnings { get; init; } = [];
}

namespace CommandCenter.Core.Continuity;

public sealed class OperationalContextReview
{
    public string ProposalId { get; init; } = string.Empty;

    public OperationalContextReviewState ReviewState { get; init; } = OperationalContextReviewState.PendingReview;

    public string BaselineCurrentContextHash { get; init; } = string.Empty;

    public string? ReviewedContentHash { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    public string? ReviewNote { get; init; }

    public string? StaleReason { get; init; }
}

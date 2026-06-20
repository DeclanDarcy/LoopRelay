namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextPromotion
{
    public string ProposalId { get; init; } = string.Empty;

    public DateTimeOffset? PromotedAt { get; init; }

    public string? PromotedContentHash { get; init; }

    public string? PromotedContentSourceRelativePath { get; init; }

    public int? RevisionNumber { get; init; }

    public string? ArchivedRelativePath { get; init; }

    public string? ArchiveFailureReason { get; init; }

    public string? WriteFailureReason { get; init; }
}

namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextProposalSummary
{
    public bool PendingProposalExists { get; init; }

    public string? LatestProposalId { get; init; }

    public DateTimeOffset? GeneratedAt { get; init; }

    public OperationalContextProposalStatus? Status { get; init; }

    public int SourceInputCount { get; init; }

    public int ContentByteCount { get; init; }

    public int ContentCharacterCount { get; init; }
}

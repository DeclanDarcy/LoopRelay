namespace LoopRelay.Core.Projections;

public sealed class RepositoryContinuitySummary
{
    public bool OperationalContextExists { get; init; }

    public int OperationalContextRevisionCount { get; init; }

    public DateTimeOffset? OperationalContextLastUpdatedAt { get; init; }

    public int OpenQuestionCount { get; init; }

    public int ActiveRiskCount { get; init; }

    public bool PendingProposalExists { get; init; }
}

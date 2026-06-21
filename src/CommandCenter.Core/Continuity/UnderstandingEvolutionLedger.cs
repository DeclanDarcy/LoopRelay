namespace CommandCenter.Core.Continuity;

public sealed class UnderstandingEvolutionLedger
{
    public IReadOnlyList<UnderstandingRevisionSnapshot> Revisions { get; init; } = [];

    public UnderstandingRevisionSnapshot? CurrentRevision => Revisions.LastOrDefault();
}

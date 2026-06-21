namespace CommandCenter.Continuity.Models;

public sealed class UnderstandingEvolutionLedger
{
    public IReadOnlyList<UnderstandingRevisionSnapshot> Revisions { get; init; } = [];

    public UnderstandingRevisionSnapshot? CurrentRevision => Revisions.LastOrDefault();
}

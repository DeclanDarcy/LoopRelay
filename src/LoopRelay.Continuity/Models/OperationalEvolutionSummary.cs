namespace LoopRelay.Continuity.Models;

public sealed class OperationalEvolutionSummary
{
    public int AddedCount { get; init; }

    public int ModifiedCount { get; init; }

    public int RemovedCount { get; init; }

    public int PreservedCount { get; init; }

    public int LostCount { get; init; }

    public int ResolvedCount { get; init; }

    public IReadOnlyList<OperationalContextSemanticChange> SemanticChanges { get; init; } = [];

    public IReadOnlyList<OperationalEvolutionTimelineEntry> TimelineEntries { get; init; } = [];

    public IReadOnlyList<ContinuityDiagnosticGroup> DiagnosticGroups { get; init; } = [];
}

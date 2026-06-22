using CommandCenter.Core.Artifacts;

namespace CommandCenter.Core.Projections;

public sealed class ArtifactInventory
{
    public Artifact? Plan { get; init; }

    public Artifact? OperationalContext { get; init; }

    public IReadOnlyList<Artifact> HistoricalOperationalContexts { get; init; } = [];

    public IReadOnlyList<Artifact> Milestones { get; init; } = [];

    public Artifact? CurrentHandoff { get; init; }

    public IReadOnlyList<Artifact> HistoricalHandoffs { get; init; } = [];

    public Artifact? CurrentDecisions { get; init; }

    public IReadOnlyList<Artifact> HistoricalDecisions { get; init; } = [];
}

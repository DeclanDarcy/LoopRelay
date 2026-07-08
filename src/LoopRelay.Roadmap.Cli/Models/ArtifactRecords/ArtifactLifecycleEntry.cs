using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

internal sealed record ArtifactLifecycleEntry(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes);

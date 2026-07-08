namespace LoopRelay.Roadmap.Cli;

internal sealed record ArtifactLifecycleEntry(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes);

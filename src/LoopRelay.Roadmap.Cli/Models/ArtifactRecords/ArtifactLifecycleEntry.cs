using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ArtifactLifecycleEntry(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes);

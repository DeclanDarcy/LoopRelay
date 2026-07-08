using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

internal sealed record ArtifactLifecycleEntryDto(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes)
{
    public static ArtifactLifecycleEntryDto FromDomain(ArtifactLifecycleEntry entry) =>
        new(entry.Path, entry.State, entry.UpdatedAt, entry.Notes);

    public ArtifactLifecycleEntry ToDomain() => new(Path, State, UpdatedAt, Notes);
}

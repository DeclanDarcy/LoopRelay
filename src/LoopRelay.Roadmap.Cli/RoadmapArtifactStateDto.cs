namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapArtifactStateDto(string Artifact, string Path, string Status)
{
    public static RoadmapArtifactStateDto FromDomain(ArtifactStateRow row) => new(row.Artifact, row.Path, row.Status);

    public ArtifactStateRow ToDomain() => new(Artifact, Path, Status);
}

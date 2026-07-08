namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ProjectionManifestCountsDto(int Valid, int Stale, int Invalid)
{
    public static ProjectionManifestCountsDto FromDomain(ProjectionManifestCounts counts) => new(counts.Valid, counts.Stale, counts.Invalid);

    public ProjectionManifestCounts ToDomain() => new(Valid, Stale, Invalid);
}

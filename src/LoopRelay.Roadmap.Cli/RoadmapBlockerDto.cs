namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapBlockerDto(string Blocker, string RequiredNextStep)
{
    public static RoadmapBlockerDto FromDomain(BlockerRow row) => new(row.Blocker, row.RequiredNextStep);

    public BlockerRow ToDomain() => new(Blocker, RequiredNextStep);
}

using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateStoreTests
{
    [Fact]
    public async Task Writes_required_sections_and_round_trips_state()
    {
        using var repo = new TempRepo();
        var store = new RoadmapStateStore(repo.Artifacts);

        await store.SaveAsync(new RoadmapStateDocument(
            RoadmapState.ActiveEpicReady,
            [new ArtifactStateRow("Epic", RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new RoadmapTransitionSummary(RoadmapState.CreateNewEpic, RoadmapState.ActiveEpicReady, "CreateNewEpic", "projection", RoadmapArtifactPaths.ActiveEpic, "Created", TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "D0001",
            1,
            0,
            new ProjectionManifestCounts(1, 0, 0),
            ["GenerateMilestoneDeepDives"],
            ["retired"]));

        string content = repo.Read(RoadmapArtifactPaths.State);
        Assert.Contains("## Current State", content, StringComparison.Ordinal);
        Assert.Contains("## Last Transition", content, StringComparison.Ordinal);
        Assert.Equal(RoadmapState.ActiveEpicReady, (await store.LoadAsync())?.CurrentState);
    }
}

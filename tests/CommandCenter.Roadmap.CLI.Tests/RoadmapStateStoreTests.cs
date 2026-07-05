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
            [new RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]));

        string content = repo.Read(RoadmapArtifactPaths.State);
        Assert.Contains("## Current State", content, StringComparison.Ordinal);
        Assert.Contains("## Last Transition", content, StringComparison.Ordinal);
        RoadmapStateDocument? loaded = await store.LoadAsync();
        Assert.Equal(RoadmapState.ActiveEpicReady, loaded?.CurrentState);
        RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
        Assert.Equal("Retired Epic", retired.EpicName);
    }

    [Fact]
    public async Task Loads_legacy_retired_exclusions_as_retired_epics_but_ignores_workflow_commands()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.State, """
            # Engineering Loop State

            ## Current State

            RetireEpic

            ## Runtime State

            ### Retired Epic Exclusions

            - Legacy Epic
            - Retire Epic
            """);

        RoadmapStateDocument? loaded = await new RoadmapStateStore(repo.Artifacts).LoadAsync();

        RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("Unknown", retired.EpicId);
        Assert.Equal("Legacy Epic", retired.EpicName);
    }
}

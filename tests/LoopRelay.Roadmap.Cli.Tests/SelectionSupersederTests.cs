using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class SelectionSupersederTests
{
    [Fact]
    public async Task SupersedeForRetiredEpicAsync_supersedes_active_selection_with_retired_epic_drift_and_lifecycle_note()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        var superseder = CreateSuperseder(repo);

        await superseder.SupersedeForRetiredEpicAsync();

        await AssertSupersededAsync(
            repo,
            Cli.DerivedArtifactStaleReason.RetiredEpicStateDrift,
            "Retired epic state changed after EpicPreparationAudit.");
    }

    [Fact]
    public async Task SupersedeForRoadmapCompletionContextAsync_supersedes_active_selection_with_context_drift_and_lifecycle_note()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        var superseder = CreateSuperseder(repo);

        await superseder.SupersedeForRoadmapCompletionContextAsync();

        await AssertSupersededAsync(
            repo,
            Cli.DerivedArtifactStaleReason.RoadmapCompletionContextDrift,
            "Roadmap completion context changed after completion certification.");
    }

    private static Cli.SelectionSuperseder CreateSuperseder(TempRepo repo) =>
        new(
            SelectionProvenanceTestSupport.CreateProvenance(repo),
            new Cli.ArtifactLifecycleStore(repo.Artifacts));

    private static async Task AssertSupersededAsync(
        TempRepo repo,
        Cli.DerivedArtifactStaleReason expectedReason,
        string expectedLifecycleNote)
    {
        Cli.SelectionProvenanceManifest manifest =
            await new Cli.SelectionProvenanceManifestStore(repo.Artifacts).LoadAsync();
        Cli.DerivedArtifactManifestEntry selection = Assert.Single(manifest.Selections);
        Assert.Equal(Cli.DerivedArtifactProvenanceStatus.Superseded, selection.ProvenanceStatus);
        Assert.Equal(Cli.DerivedArtifactFreshnessStatus.Stale, selection.FreshnessStatus);
        Assert.Contains(expectedReason, selection.FreshnessReasons);
        Assert.Empty(manifest.ActiveSelections);

        Cli.ArtifactLifecycleEntry lifecycle = Assert.Single(
            await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path == Cli.RoadmapArtifactPaths.Selection);
        Assert.Equal(Cli.ArtifactLifecycleState.Superseded, lifecycle.State);
        Assert.Equal(expectedLifecycleNote, lifecycle.Notes);
        Assert.Equal(Selection(), repo.Read(Cli.RoadmapArtifactPaths.Selection));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        return repo;
    }

    private static string Selection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | Investigate A |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;
}

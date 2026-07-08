using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Selection;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

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
            DerivedArtifactStaleReason.RetiredEpicStateDrift,
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
            DerivedArtifactStaleReason.RoadmapCompletionContextDrift,
            "Roadmap completion context changed after completion certification.");
    }

    private static SelectionSuperseder CreateSuperseder(TempRepo repo) =>
        new(
            SelectionProvenanceTestSupport.CreateProvenance(repo),
            new ArtifactLifecycleStore(repo.Artifacts));

    private static async Task AssertSupersededAsync(
        TempRepo repo,
        DerivedArtifactStaleReason expectedReason,
        string expectedLifecycleNote)
    {
        SelectionProvenanceManifest manifest =
            await new SelectionProvenanceManifestStore(repo.Artifacts).LoadAsync();
        DerivedArtifactManifestEntry selection = Assert.Single(manifest.Selections);
        Assert.Equal(DerivedArtifactProvenanceStatus.Superseded, selection.ProvenanceStatus);
        Assert.Equal(DerivedArtifactFreshnessStatus.Stale, selection.FreshnessStatus);
        Assert.Contains(expectedReason, selection.FreshnessReasons);
        Assert.Empty(manifest.ActiveSelections);

        ArtifactLifecycleEntry lifecycle = Assert.Single(
            await new ArtifactLifecycleStore(repo.Artifacts).LoadAsync(),
            entry => entry.Path == RoadmapArtifactPaths.Selection);
        Assert.Equal(ArtifactLifecycleState.Superseded, lifecycle.State);
        Assert.Equal(expectedLifecycleNote, lifecycle.Notes);
        Assert.Equal(Selection(), repo.Read(RoadmapArtifactPaths.Selection));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
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

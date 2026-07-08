using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Tests.Services.Selection;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

public sealed class ActiveSelectionReaderTests
{
    [Fact]
    public async Task Read_returns_selection_when_current_cycle_is_fresh()
    {
        using var repo = SeedRepo();
        string selection = Selection();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, selection);

        string result = await CreateReader(repo).ReadAsync(CancellationToken.None);

        Assert.Equal(selection, result);
    }

    [Fact]
    public async Task Read_rejects_missing_select_next_epic_projection()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.Selection, Selection());

        RoadmapStepException exception = await Assert.ThrowsAsync<RoadmapStepException>(
            () => CreateReader(repo).ReadAsync(CancellationToken.None));

        Assert.Equal(
            "Active selection cannot be used because its SelectNextEpic projection is missing.",
            exception.Message);
    }

    [Fact]
    public async Task Read_rejects_stale_selection_with_reasons()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Changed Completion Context");

        RoadmapStepException exception = await Assert.ThrowsAsync<RoadmapStepException>(
            () => CreateReader(repo).ReadAsync(CancellationToken.None));

        Assert.Contains(
            "Active selection cannot be used because it does not belong to the current selection cycle",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("RoadmapCompletionContextDrift", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_uses_persisted_retired_epics_when_evaluating_freshness()
    {
        using var repo = SeedRepo();
        RetiredEpic retired = new(
            "EPIC-001",
            "Retired Epic",
            "Already complete.",
            ".agents/evidence/audits/epic-preparation-audit.0001.md",
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        string selection = Selection();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, selection, [retired]);
        await SaveStateAsync(repo, [retired]);

        string result = await CreateReader(repo).ReadAsync(CancellationToken.None);

        Assert.Equal(selection, result);
    }

    private static ActiveSelectionReader CreateReader(TempRepo repo)
    {
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        return new ActiveSelectionReader(
            repo.Artifacts,
            stateStore,
            SelectionProvenanceTestSupport.CreateProvenance(repo));
    }

    private static async Task SaveStateAsync(TempRepo repo, IReadOnlyList<RetiredEpic> retiredEpics)
    {
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(new RoadmapStateDocument(
            RoadmapState.SelectNextStrategicInitiative,
            [],
            new RoadmapTransitionSummary(
                RoadmapState.CoreReady,
                RoadmapState.SelectNextStrategicInitiative,
                "SelectNextEpic",
                RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
                RoadmapArtifactPaths.Selection,
                "Completed",
                TransitionStatus.Completed,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-01-01T00:00:01Z")),
            [],
            "None",
            retiredEpics.Count,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            RoadmapTransitionIntent.Empty(RoadmapState.SelectNextStrategicInitiative),
            ["CreateNewEpic"],
            retiredEpics));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static string Selection(string initiative = "Investigate A") => $$"""
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | {{initiative}} |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;
}

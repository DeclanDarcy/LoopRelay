using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

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
        repo.Write(Cli.RoadmapArtifactPaths.Selection, Selection());

        Cli.RoadmapStepException exception = await Assert.ThrowsAsync<Cli.RoadmapStepException>(
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
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Changed Completion Context");

        Cli.RoadmapStepException exception = await Assert.ThrowsAsync<Cli.RoadmapStepException>(
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
        Cli.RetiredEpic retired = new(
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

    private static Cli.ActiveSelectionReader CreateReader(TempRepo repo)
    {
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        return new Cli.ActiveSelectionReader(
            repo.Artifacts,
            stateStore,
            SelectionProvenanceTestSupport.CreateProvenance(repo));
    }

    private static async Task SaveStateAsync(TempRepo repo, IReadOnlyList<Cli.RetiredEpic> retiredEpics)
    {
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(new Cli.RoadmapStateDocument(
            Cli.RoadmapState.SelectNextStrategicInitiative,
            [],
            new Cli.RoadmapTransitionSummary(
                Cli.RoadmapState.CoreReady,
                Cli.RoadmapState.SelectNextStrategicInitiative,
                "SelectNextEpic",
                Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
                Cli.RoadmapArtifactPaths.Selection,
                "Completed",
                Cli.TransitionStatus.Completed,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-01-01T00:00:01Z")),
            [],
            "None",
            retiredEpics.Count,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            Cli.RoadmapTransitionIntent.Empty(Cli.RoadmapState.SelectNextStrategicInitiative),
            ["CreateNewEpic"],
            retiredEpics));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
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

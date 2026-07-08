using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class RoadmapStateStoreTests
{
    [Fact]
    public async Task Writes_json_and_round_trips_state()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.RoadmapStateStore(repo.Artifacts);

        await store.SaveAsync(new RoadmapStateDocument(
            RoadmapState.ActiveEpicReady,
            [new ArtifactStateRow("Epic", RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new RoadmapTransitionSummary(RoadmapState.CreateNewEpic, RoadmapState.ActiveEpicReady, "CreateNewEpic", "projection", RoadmapArtifactPaths.ActiveEpic, "Created", TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [new BlockerRow("Historical blocker", "Keep for recovery")],
            "D0001",
            1,
            2,
            new ProjectionManifestCounts(1, 2, 3),
            new RoadmapTransitionIntent("CreateEpic", RoadmapState.ActiveEpicReady, [RoadmapArtifactPaths.ActiveEpic]),
            ["GenerateMilestoneDeepDives"],
            [new RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]));

        RoadmapStateDocument? loaded = await store.LoadAsync();
        Assert.Equal(RoadmapState.ActiveEpicReady, loaded?.CurrentState);
        Assert.Equal(RoadmapState.CreateNewEpic, loaded?.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, loaded?.LastTransition.To);
        Assert.Equal(TransitionStatus.Completed, loaded?.LastTransition.Status);
        Assert.Equal("D0001", loaded?.LastDecisionId);
        Assert.Equal(2, loaded?.SplitFamiliesCount);
        Assert.Equal(new ProjectionManifestCounts(1, 2, 3), loaded?.ProjectionManifestCounts);
        Assert.Contains(loaded!.ActiveArtifacts, row => row.Path == RoadmapArtifactPaths.ActiveEpic && row.Status == "Ready");
        BlockerRow blocker = Assert.Single(loaded.Blockers);
        Assert.Equal("Historical blocker", blocker.Blocker);
        Assert.Equal("CreateEpic", loaded?.TransitionIntent.Intent);
        Assert.Contains(RoadmapArtifactPaths.ActiveEpic, loaded!.TransitionIntent.EvidencePaths);
        Assert.Contains("GenerateMilestoneDeepDives", loaded.NextValidTransitions);
        RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
        Assert.Equal("Retired Epic", retired.EpicName);
        Assert.Contains("\"SchemaVersion\": \"roadmap-state.v1\"", repo.Read(RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
        Assert.False(Exists(repo, RoadmapArtifactPaths.State));
    }

    [Fact]
    public async Task Loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.RoadmapStateStore(repo.Artifacts);
        RoadmapStateDocument saved = StateDocument(
            RoadmapState.ExecutionBlocked,
            new RoadmapTransitionIntent(
                "Resolve|Blocker",
                RoadmapState.EvidenceBlocked,
                [".agents/evidence/blockers/very-long-path\\with|pipe<br>literal.md"]));

        await store.SaveAsync(saved);
        repo.Write(RoadmapArtifactPaths.State, "# Corrupted Projection\n\n## Current State\n\nCoreReady\n");

        RoadmapStateDocument loaded = (await store.LoadAsync())!;

        Assert.Equal(RoadmapState.ExecutionBlocked, loaded.CurrentState);
        Assert.Equal("Resolve|Blocker", loaded.TransitionIntent.Intent);
        Assert.Contains(".agents/evidence/blockers/very-long-path\\with|pipe<br>literal.md", loaded.TransitionIntent.EvidencePaths);
    }

    [Fact]
    public async Task Canonical_state_round_trips_losslessly_for_delimiter_bearing_values()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.RoadmapStateStore(repo.Artifacts);
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        RoadmapStateDocument saved = new(
            RoadmapState.EvidenceBlocked,
            [new ArtifactStateRow("Epic|Primary", ".agents\\epic|primary.md", "Ready<br>literal")],
            new RoadmapTransitionSummary(
                RoadmapState.ExecutionLoop,
                RoadmapState.EvidenceBlocked,
                "Prompt|With\\Backslash",
                ".agents/projections/a|b.md",
                ".agents/evidence/execution/very/long/path/that/contains|pipe\\slash.md",
                "Decision line 1\nDecision line 2 | pipe",
                TransitionStatus.Paused,
                timestamp,
                timestamp),
            [new BlockerRow("Blocker | with pipe", "Repair C:\\path\\with\\slashes and <br> literal")],
            "D0001|x",
            1,
            2,
            new ProjectionManifestCounts(1, 2, 3),
            new RoadmapTransitionIntent(
                "Intent | value",
                RoadmapState.EvidenceBlocked,
                [".agents/evidence/blockers/a|b.md", "C:\\evidence\\literal<br>path.md"]),
            ["Resolve blocker | rerun"],
            [new RetiredEpic("EPIC|001", "Name \\ Pipe | Epic", "Reason\nwith newline and | pipe", ".agents/evidence/audit|1.md", timestamp)]);

        await store.SaveAsync(saved);

        RoadmapStateDocument loaded = (await store.LoadAsync())!;

        Assert.Equal(saved.CurrentState, loaded.CurrentState);
        Assert.Equal("Epic|Primary", Assert.Single(loaded.ActiveArtifacts).Artifact);
        Assert.Equal("Decision line 1\nDecision line 2 | pipe", loaded.LastTransition.Decision);
        Assert.Equal("Blocker | with pipe", Assert.Single(loaded.Blockers).Blocker);
        Assert.Equal(saved.TransitionIntent.EvidencePaths, loaded.TransitionIntent.EvidencePaths);
        Assert.Equal("Reason\nwith newline and | pipe", Assert.Single(loaded.RetiredEpics).PrimaryReason);
    }

    [Fact]
    public async Task Save_is_deterministic_for_same_state_document()
    {
        using var repo = new TempRepo();
        var store = new Cli.Services.RoadmapStateStore(repo.Artifacts);
        RoadmapStateDocument state = StateDocument(RoadmapState.ActiveEpicReady, RoadmapTransitionIntent.Empty(RoadmapState.ActiveEpicReady));

        await store.SaveAsync(state);
        string firstJson = repo.Read(RoadmapArtifactPaths.StateJson);

        await store.SaveAsync(state);

        Assert.Equal(firstJson, repo.Read(RoadmapArtifactPaths.StateJson));
        Assert.False(Exists(repo, RoadmapArtifactPaths.State));
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

        RoadmapStateDocument? loaded = await new Cli.Services.RoadmapStateStore(repo.Artifacts).LoadAsync();

        RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("Unknown", retired.EpicId);
        Assert.Equal("Legacy Epic", retired.EpicName);
        Assert.True(Exists(repo, RoadmapArtifactPaths.StateJson));
    }

    [Fact]
    public async Task Malformed_legacy_markdown_fails_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.State, """
                                               # Engineering Loop State

                                               ## Current State

                                               ActiveEpicReady

                                               ## Active Artifacts

                                               | Artifact | Path | Status |
                                               |---|---|---|
                                               | Epic | .agents/epic|bad.md | Ready |
                                               """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new Cli.Services.RoadmapStateStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, RoadmapArtifactPaths.StateJson));
    }

    private static RoadmapStateDocument StateDocument(RoadmapState state, RoadmapTransitionIntent intent)
    {
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        return new RoadmapStateDocument(
            state,
            [new ArtifactStateRow("Epic", RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new RoadmapTransitionSummary(state, state, "Prompt", "Projection", "Output", "Decision", TransitionStatus.Completed, timestamp, timestamp),
            [],
            "D0001",
            0,
            0,
            new ProjectionManifestCounts(1, 0, 0),
            intent,
            ["Next"],
            []);
    }

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

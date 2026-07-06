using LoopRelay.Roadmap.Cli;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateStoreTests
{
    [Fact]
    public async Task Writes_required_sections_and_round_trips_state()
    {
        using var repo = new TempRepo();
        var store = new RoadmapStateStore(repo.Artifacts);

        await store.SaveAsync(new Cli.RoadmapStateDocument(
            Cli.RoadmapState.ActiveEpicReady,
            [new Cli.ArtifactStateRow("Epic", Cli.RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new Cli.RoadmapTransitionSummary(Cli.RoadmapState.CreateNewEpic, Cli.RoadmapState.ActiveEpicReady, "CreateNewEpic", "projection", Cli.RoadmapArtifactPaths.ActiveEpic, "Created", Cli.TransitionStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [new Cli.BlockerRow("Historical blocker", "Keep for recovery")],
            "D0001",
            1,
            2,
            new Cli.ProjectionManifestCounts(1, 2, 3),
            new Cli.RoadmapTransitionIntent("CreateEpic", Cli.RoadmapState.ActiveEpicReady, [Cli.RoadmapArtifactPaths.ActiveEpic]),
            ["GenerateMilestoneDeepDives"],
            [new Cli.RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]));

        string content = repo.Read(Cli.RoadmapArtifactPaths.State);
        Assert.Contains("## Current State", content, StringComparison.Ordinal);
        Assert.Contains("## Last Transition", content, StringComparison.Ordinal);
        Assert.Contains("## Transition Intent", content, StringComparison.Ordinal);
        Cli.RoadmapStateDocument? loaded = await store.LoadAsync();
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, loaded?.CurrentState);
        Assert.Equal(Cli.RoadmapState.CreateNewEpic, loaded?.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, loaded?.LastTransition.To);
        Assert.Equal(Cli.TransitionStatus.Completed, loaded?.LastTransition.Status);
        Assert.Equal("D0001", loaded?.LastDecisionId);
        Assert.Equal(2, loaded?.SplitFamiliesCount);
        Assert.Equal(new Cli.ProjectionManifestCounts(1, 2, 3), loaded?.ProjectionManifestCounts);
        Assert.Contains(loaded!.ActiveArtifacts, row => row.Path == Cli.RoadmapArtifactPaths.ActiveEpic && row.Status == "Ready");
        Cli.BlockerRow blocker = Assert.Single(loaded.Blockers);
        Assert.Equal("Historical blocker", blocker.Blocker);
        Assert.Equal("CreateEpic", loaded?.TransitionIntent.Intent);
        Assert.Contains(Cli.RoadmapArtifactPaths.ActiveEpic, loaded!.TransitionIntent.EvidencePaths);
        Assert.Contains("GenerateMilestoneDeepDives", loaded.NextValidTransitions);
        Cli.RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("EPIC-001", retired.EpicId);
        Assert.Equal("Retired Epic", retired.EpicName);
        Assert.Contains("\"SchemaVersion\": \"roadmap-state.v1\"", repo.Read(Cli.RoadmapArtifactPaths.StateJson), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new RoadmapStateStore(repo.Artifacts);
        Cli.RoadmapStateDocument saved = StateDocument(
            Cli.RoadmapState.ExecutionBlocked,
            new Cli.RoadmapTransitionIntent(
                "Resolve|Blocker",
                Cli.RoadmapState.EvidenceBlocked,
                [".agents/evidence/blockers/very-long-path\\with|pipe<br>literal.md"]));

        await store.SaveAsync(saved);
        repo.Write(Cli.RoadmapArtifactPaths.State, "# Corrupted Projection\n\n## Current State\n\nCoreReady\n");

        Cli.RoadmapStateDocument loaded = (await store.LoadAsync())!;

        Assert.Equal(Cli.RoadmapState.ExecutionBlocked, loaded.CurrentState);
        Assert.Equal("Resolve|Blocker", loaded.TransitionIntent.Intent);
        Assert.Contains(".agents/evidence/blockers/very-long-path\\with|pipe<br>literal.md", loaded.TransitionIntent.EvidencePaths);
    }

    [Fact]
    public async Task Canonical_state_round_trips_losslessly_for_delimiter_bearing_values()
    {
        using var repo = new TempRepo();
        var store = new RoadmapStateStore(repo.Artifacts);
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        Cli.RoadmapStateDocument saved = new(
            Cli.RoadmapState.EvidenceBlocked,
            [new Cli.ArtifactStateRow("Epic|Primary", ".agents\\epic|primary.md", "Ready<br>literal")],
            new Cli.RoadmapTransitionSummary(
                Cli.RoadmapState.ExecutionLoop,
                Cli.RoadmapState.EvidenceBlocked,
                "Prompt|With\\Backslash",
                ".agents/projections/a|b.md",
                ".agents/evidence/execution/very/long/path/that/contains|pipe\\slash.md",
                "Decision line 1\nDecision line 2 | pipe",
                Cli.TransitionStatus.Paused,
                timestamp,
                timestamp),
            [new Cli.BlockerRow("Blocker | with pipe", "Repair C:\\path\\with\\slashes and <br> literal")],
            "D0001|x",
            1,
            2,
            new Cli.ProjectionManifestCounts(1, 2, 3),
            new Cli.RoadmapTransitionIntent(
                "Intent | value",
                Cli.RoadmapState.EvidenceBlocked,
                [".agents/evidence/blockers/a|b.md", "C:\\evidence\\literal<br>path.md"]),
            ["Resolve blocker | rerun"],
            [new Cli.RetiredEpic("EPIC|001", "Name \\ Pipe | Epic", "Reason\nwith newline and | pipe", ".agents/evidence/audit|1.md", timestamp)]);

        await store.SaveAsync(saved);

        Cli.RoadmapStateDocument loaded = (await store.LoadAsync())!;

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
        var store = new RoadmapStateStore(repo.Artifacts);
        Cli.RoadmapStateDocument state = StateDocument(Cli.RoadmapState.ActiveEpicReady, Cli.RoadmapTransitionIntent.Empty(Cli.RoadmapState.ActiveEpicReady));

        await store.SaveAsync(state);
        string firstJson = repo.Read(Cli.RoadmapArtifactPaths.StateJson);
        string firstMarkdown = repo.Read(Cli.RoadmapArtifactPaths.State);

        await store.SaveAsync(state);

        Assert.Equal(firstJson, repo.Read(Cli.RoadmapArtifactPaths.StateJson));
        Assert.Equal(firstMarkdown, repo.Read(Cli.RoadmapArtifactPaths.State));
    }

    [Fact]
    public async Task Loads_legacy_retired_exclusions_as_retired_epics_but_ignores_workflow_commands()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.State, """
                                                   # Engineering Loop State

                                                   ## Current State

                                                   RetireEpic

                                                   ## Runtime State

                                                   ### Retired Epic Exclusions

                                                   - Legacy Epic
                                                   - Retire Epic
                                                   """);

        Cli.RoadmapStateDocument? loaded = await new RoadmapStateStore(repo.Artifacts).LoadAsync();

        Cli.RetiredEpic retired = Assert.Single(loaded!.RetiredEpics);
        Assert.Equal("Unknown", retired.EpicId);
        Assert.Equal("Legacy Epic", retired.EpicName);
        Assert.True(Exists(repo, Cli.RoadmapArtifactPaths.StateJson));
    }

    [Fact]
    public async Task Malformed_legacy_markdown_fails_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.State, """
                                                   # Engineering Loop State

                                                   ## Current State

                                                   ActiveEpicReady

                                                   ## Active Artifacts

                                                   | Artifact | Path | Status |
                                                   |---|---|---|
                                                   | Epic | .agents/epic|bad.md | Ready |
                                                   """);

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new RoadmapStateStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.StateJson));
    }

    private static Cli.RoadmapStateDocument StateDocument(Cli.RoadmapState state, Cli.RoadmapTransitionIntent intent)
    {
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        return new Cli.RoadmapStateDocument(
            state,
            [new Cli.ArtifactStateRow("Epic", Cli.RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new Cli.RoadmapTransitionSummary(state, state, "Prompt", "Projection", "Output", "Decision", Cli.TransitionStatus.Completed, timestamp, timestamp),
            [],
            "D0001",
            0,
            0,
            new Cli.ProjectionManifestCounts(1, 0, 0),
            intent,
            ["Next"],
            []);
    }

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapTransitionPersistenceTests
{
    [Fact]
    public async Task Save_refreshes_state_summary_and_preserves_existing_rows_when_replacements_are_omitted()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion context");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, "# Epic: Active\n");
        repo.Write(Cli.RoadmapArtifactPaths.SplitFamilyJson("alpha"), "{}");
        repo.Write(Cli.RoadmapArtifactPaths.SplitFamilyJson("beta"), "{}");

        var manifestStore = new Cli.ProjectionManifestStore(repo.Artifacts);
        await manifestStore.SaveAsync(new Cli.ProjectionManifest(
        [
            ManifestEntry("ValidPrompt", Cli.ProjectionValidationStatus.Valid, Cli.ProjectionStaleStatus.Fresh),
            ManifestEntry("StalePrompt", Cli.ProjectionValidationStatus.Valid, Cli.ProjectionStaleStatus.Stale),
            ManifestEntry("InvalidPrompt", Cli.ProjectionValidationStatus.Invalid, Cli.ProjectionStaleStatus.Fresh),
        ]));

        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        await decisionLedger.AppendAsync(new Cli.DecisionLedgerEntry(
            "D0001",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Cli.RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            [],
            [Cli.RoadmapArtifactPaths.Selection],
            "Existing Decision",
            "High",
            "Existing rationale."));
        Cli.RetiredEpic retired = new(
            "EPIC-001",
            "Retired Epic",
            "Already complete.",
            ".agents/evidence/audits/epic-preparation-audit.0001.md",
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        Cli.BlockerRow blocker = new("Historical blocker", "Keep for recovery.");
        Cli.RoadmapTransitionIntent intent = new(
            "ResolvePreviousBlocker",
            Cli.RoadmapState.EvidenceBlocked,
            [".agents/evidence/blockers/previous.0001.md"]);
        await stateStore.SaveAsync(new Cli.RoadmapStateDocument(
            Cli.RoadmapState.EvidenceBlocked,
            [],
            new Cli.RoadmapTransitionSummary(
                Cli.RoadmapState.ActiveEpicReady,
                Cli.RoadmapState.EvidenceBlocked,
                "PreviousPrompt",
                "None",
                ".agents/evidence/blockers/previous.0001.md",
                "Blocked",
                Cli.TransitionStatus.Paused,
                DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
                DateTimeOffset.Parse("2026-01-02T00:00:00Z")),
            [blocker],
            "None",
            1,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            intent,
            ["Resolve blocker and rerun"],
            [retired]));

        var persistence = new Cli.RoadmapTransitionPersistence(
            repo.Artifacts,
            manifestStore,
            stateStore,
            decisionLedger);

        await persistence.SaveAsync(
            Cli.RoadmapState.ActiveEpicReady,
            Cli.TransitionStatus.Completed,
            Cli.RoadmapState.CreateNewEpic,
            Cli.RoadmapState.ActiveEpicReady,
            "CreateNewEpic",
            Cli.RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            ".agents/one.md, .agents/two.md",
            "Completed",
            DateTimeOffset.Parse("2026-01-03T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-03T00:00:01Z"),
            null,
            null);

        Cli.RoadmapStateDocument saved = (await stateStore.LoadAsync())!;

        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, saved.CurrentState);
        Assert.Equal(Cli.RoadmapState.CreateNewEpic, saved.LastTransition.From);
        Assert.Equal(Cli.RoadmapState.ActiveEpicReady, saved.LastTransition.To);
        Assert.Equal(".agents/one.md, .agents/two.md", saved.LastTransition.Output);
        Assert.Equal("D0001", saved.LastDecisionId);
        Assert.Equal(2, saved.SplitFamiliesCount);
        Assert.Equal(new Cli.ProjectionManifestCounts(2, 1, 1), saved.ProjectionManifestCounts);
        Assert.Equal(intent.Intent, saved.TransitionIntent.Intent);
        Assert.Equal(intent.DispatchState, saved.TransitionIntent.DispatchState);
        Assert.Equal(intent.EvidencePaths, saved.TransitionIntent.EvidencePaths);
        Assert.Equal(["GenerateMilestoneDeepDives"], saved.NextValidTransitions);
        Assert.Equal(retired, Assert.Single(saved.RetiredEpics));
        Assert.Equal(blocker, Assert.Single(saved.Blockers));
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == Cli.RoadmapArtifactPaths.RoadmapCompletionContext && row.Status == "Present");
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == Cli.RoadmapArtifactPaths.Selection && row.Status == "Missing");
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == Cli.RoadmapArtifactPaths.ActiveEpic && row.Status == "Present");
    }

    [Fact]
    public void Parse_output_evidence_paths_preserves_non_none_paths()
    {
        IReadOnlyList<string> paths = Cli.RoadmapTransitionPersistence.ParseOutputEvidencePaths(
            "None, .agents/evidence/execution/one.md, .agents/evidence/blockers/two.md");

        Assert.Equal(
            [".agents/evidence/execution/one.md", ".agents/evidence/blockers/two.md"],
            paths);
    }

    private static Cli.ProjectionManifestEntry ManifestEntry(
        string prompt,
        Cli.ProjectionValidationStatus validationStatus,
        Cli.ProjectionStaleStatus staleStatus) =>
        new(
            prompt,
            $"ProjectionFor{prompt}",
            $".agents/projections/{prompt}.md",
            "source-hash",
            [],
            "context-hash",
            $"projection-hash-{prompt}",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            validationStatus,
            staleStatus,
            null);
}

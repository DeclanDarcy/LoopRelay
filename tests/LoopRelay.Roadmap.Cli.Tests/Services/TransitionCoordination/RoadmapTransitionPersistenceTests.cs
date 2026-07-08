using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

public sealed class RoadmapTransitionPersistenceTests
{
    [Fact]
    public async Task Save_refreshes_state_summary_and_preserves_existing_rows_when_replacements_are_omitted()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion context");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "# Epic: Active\n");
        repo.Write(RoadmapArtifactPaths.SplitFamilyJson("alpha"), "{}");
        repo.Write(RoadmapArtifactPaths.SplitFamilyJson("beta"), "{}");

        var manifestStore = new ProjectionManifestStore(repo.Artifacts);
        await manifestStore.SaveAsync(new ProjectionManifest(
        [
            ManifestEntry("ValidPrompt", ProjectionValidationStatus.Valid, ProjectionStaleStatus.Fresh),
            ManifestEntry("StalePrompt", ProjectionValidationStatus.Valid, ProjectionStaleStatus.Stale),
            ManifestEntry("InvalidPrompt", ProjectionValidationStatus.Invalid, ProjectionStaleStatus.Fresh),
        ]));

        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        await decisionLedger.AppendAsync(new DecisionLedgerEntry(
            "D0001",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            [],
            [RoadmapArtifactPaths.Selection],
            "Existing Decision",
            "High",
            "Existing rationale."));
        RetiredEpic retired = new(
            "EPIC-001",
            "Retired Epic",
            "Already complete.",
            ".agents/evidence/audits/epic-preparation-audit.0001.md",
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        BlockerRow blocker = new("Historical blocker", "Keep for recovery.");
        RoadmapTransitionIntent intent = new(
            "ResolvePreviousBlocker",
            RoadmapState.EvidenceBlocked,
            [".agents/evidence/blockers/previous.0001.md"]);
        await stateStore.SaveAsync(new RoadmapStateDocument(
            RoadmapState.EvidenceBlocked,
            [],
            new RoadmapTransitionSummary(
                RoadmapState.ActiveEpicReady,
                RoadmapState.EvidenceBlocked,
                "PreviousPrompt",
                "None",
                ".agents/evidence/blockers/previous.0001.md",
                "Blocked",
                TransitionStatus.Paused,
                DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
                DateTimeOffset.Parse("2026-01-02T00:00:00Z")),
            [blocker],
            "None",
            1,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            intent,
            ["Resolve blocker and rerun"],
            [retired]));

        var persistence = new RoadmapTransitionPersistence(
            repo.Artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            new TransitionJournalStore(repo.Artifacts));

        await persistence.SaveAsync(
            RoadmapState.ActiveEpicReady,
            TransitionStatus.Completed,
            RoadmapState.CreateNewEpic,
            RoadmapState.ActiveEpicReady,
            "CreateNewEpic",
            RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"],
            ".agents/one.md, .agents/two.md",
            "Completed",
            DateTimeOffset.Parse("2026-01-03T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-03T00:00:01Z"),
            null,
            null);

        RoadmapStateDocument saved = (await stateStore.LoadAsync())!;

        Assert.Equal(RoadmapState.ActiveEpicReady, saved.CurrentState);
        Assert.Equal(RoadmapState.CreateNewEpic, saved.LastTransition.From);
        Assert.Equal(RoadmapState.ActiveEpicReady, saved.LastTransition.To);
        Assert.Equal(".agents/one.md, .agents/two.md", saved.LastTransition.Output);
        Assert.Equal("D0001", saved.LastDecisionId);
        Assert.Equal(2, saved.SplitFamiliesCount);
        Assert.Equal(new ProjectionManifestCounts(2, 1, 1), saved.ProjectionManifestCounts);
        Assert.Equal(intent.Intent, saved.TransitionIntent.Intent);
        Assert.Equal(intent.DispatchState, saved.TransitionIntent.DispatchState);
        Assert.Equal(intent.EvidencePaths, saved.TransitionIntent.EvidencePaths);
        Assert.Equal(["GenerateMilestoneDeepDives"], saved.NextValidTransitions);
        Assert.Equal(retired, Assert.Single(saved.RetiredEpics));
        Assert.Equal(blocker, Assert.Single(saved.Blockers));
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == RoadmapArtifactPaths.RoadmapCompletionContext && row.Status == "Present");
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == RoadmapArtifactPaths.Selection && row.Status == "Missing");
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == RoadmapArtifactPaths.ActiveEpic && row.Status == "Present");
    }

    [Fact]
    public async Task Persist_workflow_failure_writes_recovery_state_and_journal_record()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "# Epic: Active\n");

        var manifestStore = new ProjectionManifestStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var journalStore = new TransitionJournalStore(repo.Artifacts);
        var persistence = new RoadmapTransitionPersistence(
            repo.Artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            journalStore);

        DateTimeOffset failedAt = DateTimeOffset.Parse("2026-01-04T00:00:00Z");
        string[] evidencePaths =
        [
            ".agents/evidence/orchestration/invariant.0001.md",
            ".agents/evidence/blockers/fallback.0001.md",
        ];
        var failure = new RoadmapWorkflowFailure(
            "InvariantFailed",
            RoadmapState.ActiveEpicReady,
            RoadmapState.MilestoneSpecsReady,
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            "GenerateMilestoneDeepDivesForEpic",
            RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"],
            "InvariantValidator",
            "SpecEpicMismatch",
            evidencePaths,
            "Spec epic path does not match the active epic.",
            "Repair the milestone spec bundle.",
            "ResolveInvariantViolation",
            "Invariant Failed: SpecEpicMismatch",
            failedAt);

        await persistence.PersistWorkflowFailureAsync(failure);

        RoadmapStateDocument saved = (await stateStore.LoadAsync())!;
        Assert.Equal(RoadmapState.EvidenceBlocked, saved.CurrentState);
        Assert.Equal(TransitionStatus.Paused, saved.LastTransition.Status);
        Assert.Equal(RoadmapState.ActiveEpicReady, saved.LastTransition.From);
        Assert.Equal(RoadmapState.MilestoneSpecsReady, saved.LastTransition.To);
        Assert.Equal("GenerateMilestoneDeepDivesForEpic", saved.LastTransition.Prompt);
        Assert.Equal((string?)RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"], saved.LastTransition.Projection);
        Assert.Equal(string.Join(", ", evidencePaths), saved.LastTransition.Output);
        Assert.Equal("Invariant Failed: SpecEpicMismatch", saved.LastTransition.Decision);
        Assert.Equal(failedAt, saved.LastTransition.StartedAt);
        Assert.Equal(failedAt, saved.LastTransition.CompletedAt);
        BlockerRow blocker = Assert.Single(saved.Blockers);
        Assert.Equal("Spec epic path does not match the active epic.", blocker.Blocker);
        Assert.Equal("Repair the milestone spec bundle.", blocker.RequiredNextStep);
        Assert.Equal("ResolveInvariantViolation", saved.TransitionIntent.Intent);
        Assert.Equal(RoadmapState.EvidenceBlocked, saved.TransitionIntent.DispatchState);
        Assert.Equal(evidencePaths, saved.TransitionIntent.EvidencePaths);
        Assert.Equal(["Review invariant failure evidence and rerun"], saved.NextValidTransitions);
        Assert.Contains(saved.ActiveArtifacts, row => row.Path == RoadmapArtifactPaths.ActiveEpic && row.Status == "Present");

        TransitionJournalRecord journal = JsonSerializer.Deserialize<TransitionJournalRecord>(
            repo.Read(RoadmapArtifactPaths.TransitionJournal),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("InvariantFailed", journal.Event);
        Assert.Equal(RoadmapState.ActiveEpicReady, journal.PreviousState);
        Assert.Equal(RoadmapState.MilestoneSpecsReady, journal.AttemptedState);
        Assert.Equal("GenerateMilestoneDeepDivesForEpic", journal.Prompt);
        Assert.Equal("InvariantValidator", journal.PromptContractKey);
        Assert.Equal(evidencePaths, journal.OutputPaths);
        Assert.Equal("EvidenceBlocked", journal.Result);
        Assert.Equal("SpecEpicMismatch", journal.ParserDecision);
        Assert.Equal("Spec epic path does not match the active epic.", journal.ErrorMessage);
    }

    [Fact]
    public void Parse_output_evidence_paths_preserves_non_none_paths()
    {
        IReadOnlyList<string> paths = RoadmapTransitionPersistence.ParseOutputEvidencePaths(
            "None, .agents/evidence/execution/one.md, .agents/evidence/blockers/two.md");

        Assert.Equal(
            [".agents/evidence/execution/one.md", ".agents/evidence/blockers/two.md"],
            paths);
    }

    private static ProjectionManifestEntry ManifestEntry(
        string prompt,
        ProjectionValidationStatus validationStatus,
        ProjectionStaleStatus staleStatus) =>
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

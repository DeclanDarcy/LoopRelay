using System.Text.Json;
using LoopRelay.Orchestration.Services;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Persistence;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class WorkspaceFilesystemSnapshotTests
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Snapshot_imports_and_exports_migrated_files_deterministically()
    {
        using var source = new TempRepo();
        await SeedCanonicalMigratedFilesAsync(source);
        string[] migratedPaths =
        [
            RoadmapArtifactPaths.DecisionLedgerJson,
            RoadmapArtifactPaths.StateJson,
            RoadmapArtifactPaths.LifecycleJson,
            RoadmapArtifactPaths.SplitFamilyJson("family-1"),
            RoadmapArtifactPaths.ExecutionPreparationManifest,
            RoadmapArtifactPaths.SelectionProvenanceManifest,
            RoadmapArtifactPaths.ProjectionsManifestJson,
            RoadmapArtifactPaths.TransitionJournal,
            OrchestrationArtifactPaths.HistoricalDecision(1),
            OrchestrationArtifactPaths.HistoricalHandoff(2),
            OrchestrationArtifactPaths.HistoricalDelta(3),
            $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0003.md",
        ];

        var store = new WorkspaceFilesystemSnapshotStore();
        WorkspaceFilesystemSnapshot snapshot = await store.ImportAsync(source.Artifacts);

        using var exported = new TempRepo();
        await store.ExportAsync(exported.Artifacts, snapshot);

        foreach (string path in migratedPaths)
        {
            Assert.Equal(source.Read(path), exported.Read(path));
        }

        WorkspaceFilesystemSnapshot reimported = await store.ImportAsync(exported.Artifacts);
        using var reexported = new TempRepo();
        await store.ExportAsync(reexported.Artifacts, reimported);

        foreach (string path in migratedPaths)
        {
            Assert.Equal(exported.Read(path), reexported.Read(path));
        }
    }

    [Fact]
    public async Task Optional_execution_and_selection_manifests_import_as_empty_when_missing()
    {
        using var repo = new TempRepo();

        WorkspaceFilesystemSnapshot snapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts);

        Assert.Equal(ExecutionPreparationManifest.CurrentSchemaVersion, snapshot.ExecutionPreparationManifest.SchemaVersion);
        Assert.Empty(snapshot.ExecutionPreparationManifest.Artifacts);
        Assert.Empty(snapshot.ExecutionPreparationManifest.MilestoneSpecs);
        Assert.Equal(SelectionProvenanceManifest.CurrentSchemaVersion, snapshot.SelectionProvenanceManifest.SchemaVersion);
        Assert.Empty(snapshot.SelectionProvenanceManifest.Selections);
    }

    [Fact]
    public async Task Legacy_markdown_import_preserves_json_authority_without_mutating_source()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.DecisionLedger, """
                                                        # Decision Ledger

                                                        ## D0007

                                                        | Field | Value |
                                                        |---|---|
                                                        | Timestamp | 2026-01-01T00:00:00.0000000+00:00 |
                                                        | State | SelectNextStrategicInitiative |
                                                        | Transition | SelectNextEpic |
                                                        | Prompt | SelectNextEpic |
                                                        | Projection Path | projection |
                                                        | Input Artifact Paths | input |
                                                        | Output Artifact Paths | output |
                                                        | Decision / Disposition | Select Existing Epic |
                                                        | Confidence | High |
                                                        | Rationale Excerpt | reason |
                                                        """);

        WorkspaceFilesystemSnapshot snapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts);

        DecisionLedgerEntryDto entry = Assert.Single(snapshot.DecisionLedger.Entries);
        Assert.Equal("D0007", entry.DecisionId);
        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.DecisionLedgerJson));
    }

    [Fact]
    public async Task Duplicate_migrated_identity_fails_snapshot_import()
    {
        using var repo = new TempRepo();
        DecisionLedgerPersistenceDocument duplicate = DecisionLedgerPersistenceDocument.FromDomain(
            [Decision("D0001"), Decision("D0001")]);
        repo.Write(RoadmapArtifactPaths.DecisionLedgerJson, JsonSerializer.Serialize(duplicate, RoadmapJson.Options) + Environment.NewLine);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts));

        Assert.Contains("decision ledger", ex.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate decision ID `D0001`", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_history_sequence_filename_fails_snapshot_import()
    {
        using var repo = new TempRepo();
        repo.Write($"{OrchestrationArtifactPaths.DecisionsDirectory}/decisions.abcd.md", "invalid");

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts));

        Assert.Contains("loop history", ex.Message, StringComparison.Ordinal);
        Assert.Contains("positive four-digit sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ambiguous_retained_paths_are_not_migrated_by_snapshot_export()
    {
        using var source = new TempRepo();
        source.Write(".agents/core/01-purpose.md", "not an implemented migrated input");
        source.Write(".agents/evals/evaluation.md", "not an implemented migrated path");

        var store = new WorkspaceFilesystemSnapshotStore();
        WorkspaceFilesystemSnapshot snapshot = await store.ImportAsync(source.Artifacts);

        using var exported = new TempRepo();
        await store.ExportAsync(exported.Artifacts, snapshot);

        Assert.False(await exported.Artifacts.ExistsAsync(".agents/core/01-purpose.md"));
        Assert.False(await exported.Artifacts.ExistsAsync(".agents/evals/evaluation.md"));
    }

    private static async Task SeedCanonicalMigratedFilesAsync(TempRepo repo)
    {
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0001"));

        await new RoadmapStateStore(repo.Artifacts).SaveAsync(new RoadmapStateDocument(
            RoadmapState.CoreReady,
            [new ArtifactStateRow("Epic", RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new RoadmapTransitionSummary(
                RoadmapState.CoreReady,
                RoadmapState.SelectNextStrategicInitiative,
                "SelectNextEpic",
                RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
                RoadmapArtifactPaths.Selection,
                "D0001",
                TransitionStatus.Completed,
                Instant,
                Instant),
            [new BlockerRow("None", "None")],
            "D0001",
            0,
            1,
            new ProjectionManifestCounts(1, 0, 0),
            RoadmapTransitionIntent.Empty(RoadmapState.SelectNextStrategicInitiative),
            [nameof(RoadmapState.SelectNextStrategicInitiative)],
            [new RetiredEpic("EPIC-1", "Done", "Complete", ".agents/evidence/evaluations/done.md", Instant)]));

        await new ArtifactLifecycleStore(repo.Artifacts).SaveAsync(
            [new ArtifactLifecycleEntry(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready, Instant, "ready")]);

        await new SplitFamilyStore(repo.Artifacts).WriteAsync(new SplitFamily(
            "family-1",
            "Split proposal",
            [".agents/epic-a.md", ".agents/epic-b.md"],
            [".agents/epic-a.md", ".agents/epic-b.md"],
            ".agents/epic-a.md",
            "Best next child",
            Instant));

        await new ExecutionPreparationManifestStore(repo.Artifacts).SaveAsync(new ExecutionPreparationManifest(
            ExecutionPreparationManifest.CurrentSchemaVersion,
            RoadmapArtifactPaths.ActiveEpic,
            "active-hash",
            [new ExecutionPreparationManifestInput("MilestoneSpec", ".agents/specs/s1.md", "spec-hash")],
            [DerivedEntry("ExecutionPrompt", RoadmapArtifactPaths.ExecutionPrompt)]));

        await new SelectionProvenanceManifestStore(repo.Artifacts).SaveAsync(SelectionProvenanceManifest.Empty.UpsertActive(
            DerivedEntry("Selection", RoadmapArtifactPaths.Selection)));

        await new ProjectionManifestStore(repo.Artifacts).SaveAsync(new ProjectionManifest(
            [new ProjectionManifestEntry(
                "SelectNextEpic",
                "ProjectionForSelectNextEpic",
                RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
                "prompt-source-hash",
                RoadmapArtifactPaths.ProjectContextSourceFiles,
                "context-hash",
                "projection-hash",
                Instant,
                ProjectionValidationStatus.Valid,
                ProjectionStaleStatus.Fresh,
                null,
                ProjectionProvenanceStatus.Trusted,
                "SelectNextEpic",
                "Selection",
                [],
                [])]));

        await new TransitionJournalStore(repo.Artifacts).AppendAsync(new TransitionJournalRecord(
            "TransitionCompleted",
            "correlation-1",
            Instant,
            RoadmapState.CoreReady,
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            "contract",
            new Dictionary<string, string> { [RoadmapArtifactPaths.ActiveEpic] = "active-hash" },
            [RoadmapArtifactPaths.Selection],
            123,
            "Completed",
            "Select Existing Epic",
            null));

        repo.Write(OrchestrationArtifactPaths.HistoricalDecision(1), "decision body\r\nwith exact text");
        repo.Write(OrchestrationArtifactPaths.HistoricalHandoff(2), "handoff body");
        repo.Write(OrchestrationArtifactPaths.HistoricalDelta(3), "delta body\n");
        repo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0003.md", "evidence body\r\n");
    }

    private static DecisionLedgerEntry Decision(string decisionId) =>
        new(
            decisionId,
            Instant,
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            [RoadmapArtifactPaths.ActiveEpic],
            [RoadmapArtifactPaths.Selection],
            "Select Existing Epic",
            "High",
            "Reason");

    private static DerivedArtifactManifestEntry DerivedEntry(string kind, string identity) =>
        new(
            kind,
            identity,
            identity,
            "test",
            "artifact-hash",
            Instant,
            DerivedArtifactProvenanceStatus.Trusted,
            [new DerivedArtifactCausalInput("Input", RoadmapArtifactPaths.ActiveEpic, "active-hash")],
            DerivedArtifactFreshnessStatus.Fresh,
            []);
}

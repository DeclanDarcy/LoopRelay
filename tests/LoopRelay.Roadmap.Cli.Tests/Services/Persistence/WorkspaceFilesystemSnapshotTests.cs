using System.Text.Json;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
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
        repo.Write(RoadmapArtifactPaths.DecisionLedgerJson, SerializeStructured(duplicate));

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts));

        Assert.Contains("decision ledger", ex.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate decision ID `D0001`", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_structured_domain_identities_fail_snapshot_import()
    {
        using var lifecycleRepo = new TempRepo();
        lifecycleRepo.Write(
            RoadmapArtifactPaths.LifecycleJson,
            SerializeStructured(ArtifactLifecyclePersistenceDocument.FromDomain(
            [
                new ArtifactLifecycleEntry(".agents/epic.md", ArtifactLifecycleState.Ready, Instant, "ready"),
                new ArtifactLifecycleEntry(".agents/EPIC.md", ArtifactLifecycleState.Draft, Instant, "draft"),
            ])));

        RoadmapStepException lifecycle = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(lifecycleRepo.Artifacts));

        Assert.Contains("artifact lifecycle", lifecycle.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate path", lifecycle.Message, StringComparison.Ordinal);

        using var projectionRepo = new TempRepo();
        projectionRepo.Write(
            RoadmapArtifactPaths.ProjectionsManifestJson,
            SerializeStructured(ProjectionManifestPersistenceDocument.FromDomain(new ProjectionManifest(
            [
                ProjectionEntry("SelectNextEpic", ".agents/projections/select-next-epic.md"),
                ProjectionEntry("SelectNextEpic", ".agents/projections/select-next-epic-alt.md"),
            ]))));

        RoadmapStepException projection = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(projectionRepo.Artifacts));

        Assert.Contains("projection manifest", projection.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate runtime prompt `SelectNextEpic`", projection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_listed_sequence_and_family_id_fail_snapshot_import()
    {
        string root = NewVirtualRoot();
        var historyStore = new ListedArtifactStore(root);
        historyStore.Write(OrchestrationArtifactPaths.HistoricalDecision(1), "decision");
        historyStore.SetList(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            OrchestrationArtifactPaths.HistoricalDecision(1),
            OrchestrationArtifactPaths.HistoricalDecision(1));

        RoadmapStepException history = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(ArtifactsFor(historyStore, root)));

        Assert.Contains("loop history", history.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate history sequence `0001`", history.Message, StringComparison.Ordinal);

        string splitRoot = NewVirtualRoot();
        var splitStore = new ListedArtifactStore(splitRoot);
        string splitPath = RoadmapArtifactPaths.SplitFamilyJson("family-1");
        splitStore.Write(splitPath, SerializeStructured(SplitFamilyPersistenceDocument.FromDomain(SplitFamily("family-1"))));
        splitStore.SetList(
            RoadmapArtifactPaths.SplitFamiliesDirectory,
            "split-family-*.json",
            splitPath,
            splitPath);

        RoadmapStepException split = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(ArtifactsFor(splitStore, splitRoot)));

        Assert.Contains("split lineage", split.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate split family ID `family-1`", split.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_and_malformed_exports_fail_snapshot_import()
    {
        using var partialRepo = new TempRepo();
        partialRepo.Write(RoadmapArtifactPaths.DecisionLedgerJson, """
                                                                   {
                                                                     "SchemaVersion": "decision-ledger.v1",
                                                                     "Entries": null
                                                                   }
                                                                   """);

        RoadmapStepException partial = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(partialRepo.Artifacts));

        Assert.Contains("decision ledger", partial.Message, StringComparison.Ordinal);
        Assert.Contains("Decision ledger entries are required.", partial.Message, StringComparison.Ordinal);

        using var malformedRepo = new TempRepo();
        malformedRepo.Write(RoadmapArtifactPaths.StateJson, "{");

        RoadmapStepException malformed = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(malformedRepo.Artifacts));

        Assert.Contains("roadmap state", malformed.Message, StringComparison.Ordinal);
        Assert.Contains("invalid JSON", malformed.Message, StringComparison.Ordinal);
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
    public async Task Invalid_or_missing_execution_evidence_export_fails_snapshot_import()
    {
        using var invalidRepo = new TempRepo();
        invalidRepo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.abcd.md", "invalid");

        RoadmapStepException invalid = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(invalidRepo.Artifacts));

        Assert.Contains("execution evidence", invalid.Message, StringComparison.Ordinal);
        Assert.Contains("positive four-digit sequence", invalid.Message, StringComparison.Ordinal);

        string root = NewVirtualRoot();
        var missingStore = new ListedArtifactStore(root);
        missingStore.SetList(
            RoadmapArtifactPaths.ExecutionEvidenceDirectory,
            "*.md",
            $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0001.md");

        RoadmapStepException missing = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new WorkspaceFilesystemSnapshotStore().ImportAsync(ArtifactsFor(missingStore, root)));

        Assert.Contains("execution evidence", missing.Message, StringComparison.Ordinal);
        Assert.Contains("listed file could not be read", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_markdown_only_snapshot_imports_legacy_capable_domains_without_mutating_source()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.State, """
                                               # Engineering Loop State

                                               ## Current State

                                               CoreReady
                                               """);
        repo.Write(RoadmapArtifactPaths.Lifecycle, """
                                                   # Artifact Lifecycle

                                                   | Path | State | Updated At | Notes |
                                                   |---|---|---|---|
                                                   | .agents/epic.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | legacy |
                                                   """);
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | source-hash | .agents/ctx/01-purpose.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                             """);
        repo.Write(RoadmapArtifactPaths.SplitFamily("legacy"), LegacySplitFamilyMarkdown("legacy"));

        WorkspaceFilesystemSnapshot snapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts);

        Assert.NotNull(snapshot.RoadmapState);
        Assert.Equal(RoadmapState.CoreReady, snapshot.RoadmapState!.CurrentState);
        ArtifactLifecycleEntryDto lifecycle = Assert.Single(snapshot.ArtifactLifecycle.Entries);
        Assert.Equal(".agents/epic.md", lifecycle.Path);
        ProjectionManifestEntryDto projection = Assert.Single(snapshot.ProjectionManifest.Entries);
        Assert.Equal("SelectNextEpic", projection.RuntimePromptName);
        SplitFamilyFilesystemSnapshot split = Assert.Single(snapshot.SplitFamilies);
        Assert.Equal("legacy", split.FamilyId);

        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.StateJson));
        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.LifecycleJson));
        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.ProjectionsManifestJson));
        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.SplitFamilyJson("legacy")));
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

    private static string SerializeStructured<TDocument>(TDocument document) =>
        JsonSerializer.Serialize(document, RoadmapJson.Options) + Environment.NewLine;

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

    private static ProjectionManifestEntry ProjectionEntry(string runtimePromptName, string projectionPath) =>
        new(
            runtimePromptName,
            "ProjectionForSelectNextEpic",
            projectionPath,
            "prompt-source-hash",
            RoadmapArtifactPaths.ProjectContextSourceFiles,
            "context-hash",
            "projection-hash",
            Instant,
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Fresh,
            null,
            ProjectionProvenanceStatus.Trusted,
            runtimePromptName,
            "Selection",
            [],
            []);

    private static SplitFamily SplitFamily(string familyId) =>
        new(
            familyId,
            "Split proposal",
            [".agents/epic-a.md", ".agents/epic-b.md"],
            [".agents/epic-a.md", ".agents/epic-b.md"],
            ".agents/epic-a.md",
            "Best next child",
            Instant);

    private static string LegacySplitFamilyMarkdown(string familyId) =>
        $$"""
          # Split Family

          | Field | Value |
          |---|---|
          | Family ID | {{familyId}} |
          | Created At | 2026-01-01T00:00:00.0000000+00:00 |
          | Selected Child | .agents/epic-2.md |
          | Selected Child Rationale | unblock |

          ## Proposal

          Split this epic.

          ## Child Epics

          - .agents/epic-1.md
          - .agents/epic-2.md

          ## Dependency Order

          - .agents/epic-1.md
          - .agents/epic-2.md
          """;

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

    private static string NewVirtualRoot() =>
        Path.Combine(Path.GetTempPath(), "cc-roadmap-virtual", Guid.NewGuid().ToString("N"));

    private static RoadmapArtifacts ArtifactsFor(IArtifactStore store, string root) =>
        new(store, new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = root,
        });

    private sealed class ListedArtifactStore(string root) : IArtifactStore
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string Directory, string Pattern), IReadOnlyList<string>> _lists = [];

        public void Write(string relativePath, string content) =>
            _files[NormalizeRelative(relativePath)] = content;

        public void SetList(string relativeDirectory, string searchPattern, params string[] relativePaths) =>
            _lists[(NormalizeRelative(relativeDirectory), searchPattern)] = relativePaths
                .Select(ToFullPath)
                .ToArray();

        public Task<bool> ExistsAsync(string path) =>
            Task.FromResult(_files.ContainsKey(ToRelativePath(path)));

        public Task<string?> ReadAsync(string path)
        {
            _files.TryGetValue(ToRelativePath(path), out string? content);
            return Task.FromResult(content);
        }

        public Task<T?> ReadAs<T>(string path, Func<string, T?> deserialize) =>
            Task.FromResult(_files.TryGetValue(ToRelativePath(path), out string? content) ? deserialize(content) : default);

        public Task WriteAsync(string path, string content)
        {
            _files[ToRelativePath(path)] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path)
        {
            _files.Remove(ToRelativePath(path));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
        {
            return Task.FromResult(
                _lists.TryGetValue((ToRelativePath(path), searchPattern), out IReadOnlyList<string>? paths)
                    ? paths
                    : Array.Empty<string>());
        }

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        private string ToFullPath(string relativePath) =>
            Path.GetFullPath(Path.Combine(root, NormalizeRelative(relativePath).Replace('/', Path.DirectorySeparatorChar)));

        private string ToRelativePath(string path) =>
            NormalizeRelative(Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path)));

        private static string NormalizeRelative(string path) =>
            path.Replace('\\', '/').TrimStart('/');
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Services.Persistence;
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
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class WorkspaceSqliteStoreTests
{
    private static readonly DateTimeOffset Instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Missing_database_initializes_to_valid_empty_state()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);

        Assert.False(File.Exists(databasePath));
        WorkspaceSqliteOperationResult init = await sqlite.InitializeAsync(repo.Repository);
        WorkspaceDatabaseIntegrityResult validation = await sqlite.ValidateAsync(repo.Repository);

        Assert.Equal(WorkspaceStorageResultCategory.Initialized, init.Category);
        Assert.Equal(WorkspaceDatabaseIntegrityStatus.ValidEmpty, validation.Status);
        Assert.True(File.Exists(databasePath));
        Assert.StartsWith(repo.Root, databasePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Full_filesystem_snapshot_imports_to_logically_equivalent_database()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        WorkspaceFilesystemSnapshot sourceSnapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts);
        var sqlite = new WorkspaceSqliteStore();

        WorkspaceSqliteOperationResult result = await sqlite.ImportAsync(repo.Artifacts);
        WorkspaceFilesystemSnapshot databaseSnapshot = await sqlite.ReadSnapshotAsync(repo.Repository);
        WorkspaceDatabaseIntegrityResult validation = await sqlite.ValidateAsync(repo.Repository);

        Assert.Equal(WorkspaceStorageResultCategory.Imported, result.Category);
        Assert.Equal(WorkspaceDatabaseIntegrityStatus.ValidImported, validation.Status);
        Assert.Equal(SnapshotJson(sourceSnapshot), SnapshotJson(databaseSnapshot));
    }

    [Fact]
    public async Task Reimport_with_unchanged_source_is_idempotent()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        var sqlite = new WorkspaceSqliteStore();

        await sqlite.ImportAsync(repo.Artifacts);
        string first = SnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository));
        await sqlite.ImportAsync(repo.Artifacts);
        string second = SnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository));

        Assert.Equal(first, second);
        Assert.Equal(WorkspaceDatabaseIntegrityStatus.ValidImported, (await sqlite.ValidateAsync(repo.Repository)).Status);
    }

    [Fact]
    public async Task Import_failure_rolls_back_existing_database_state()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        string before = SnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository));
        repo.Write(
            RoadmapArtifactPaths.DecisionLedgerJson,
            JsonSerializer.Serialize(
                DecisionLedgerPersistenceDocument.FromDomain([Decision("D0001"), Decision("D0001")]),
                RoadmapJson.Options) + Environment.NewLine);

        await Assert.ThrowsAsync<RoadmapStepException>(() => sqlite.ImportAsync(repo.Artifacts));

        Assert.Equal(WorkspaceDatabaseIntegrityStatus.ValidImported, (await sqlite.ValidateAsync(repo.Repository)).Status);
        Assert.Equal(before, SnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository)));
    }

    [Fact]
    public async Task Unsupported_schema_version_blocks_validation_without_repair()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(databasePath, "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';");

        WorkspaceDatabaseIntegrityResult validation = await sqlite.ValidateAsync(repo.Repository);
        string schema = await ScalarStringAsync(databasePath, "SELECT value FROM schema_metadata WHERE key = 'schema_version';");

        Assert.Equal(WorkspaceDatabaseIntegrityStatus.UnsupportedSchema, validation.Status);
        Assert.Equal("999", schema);
    }

    [Fact]
    public async Task Corrupt_database_row_classifies_as_corrupt()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(
            databasePath,
            """
            INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at)
            VALUES ('Decisions', 1, '.agents/decisions/decisions.0001.md', 'body', 'wrong-hash', '2026-01-01T00:00:00.0000000+00:00');
            """);

        WorkspaceDatabaseIntegrityResult validation = await sqlite.ValidateAsync(repo.Repository);

        Assert.Equal(WorkspaceDatabaseIntegrityStatus.Corrupt, validation.Status);
        Assert.Contains("hash mismatch", validation.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task File_backed_workflow_stores_remain_authoritative_after_database_import()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        RoadmapStateDocument fileBackedState = State(RoadmapState.EvidenceBlocked);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(fileBackedState);

        RoadmapStateDocument loaded = (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        WorkspaceFilesystemSnapshot databaseSnapshot = await sqlite.ReadSnapshotAsync(repo.Repository);

        Assert.Equal(RoadmapState.EvidenceBlocked, loaded.CurrentState);
        Assert.Equal(RoadmapState.CoreReady, databaseSnapshot.RoadmapState!.CurrentState);
    }

    [Fact]
    public async Task SQLite_core_stores_load_after_exported_core_files_are_deleted_and_regenerate_exports()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await DeleteCoreExportsAsync(repo);

        RoadmapStateDocument loaded = (await new SqliteRoadmapStateStore(repo.Repository).LoadAsync())!;
        WorkspaceSqliteOperationResult export = await sqlite.ExportCoreAsync(repo.Artifacts);

        Assert.Equal(RoadmapState.CoreReady, loaded.CurrentState);
        Assert.Equal(WorkspaceStorageResultCategory.Exported, export.Category);
        Assert.True(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.DecisionLedgerJson));
        Assert.True(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.StateJson));
        Assert.True(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.LifecycleJson));
        Assert.True(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.SplitFamilyJson("family-1")));
    }

    [Fact]
    public async Task SQLite_core_state_ignores_stale_filesystem_json_export()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await new WorkspaceSqliteStore().ImportAsync(repo.Artifacts);
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(RoadmapState.EvidenceBlocked));

        RoadmapStateDocument sqliteState = (await new SqliteRoadmapStateStore(repo.Repository).LoadAsync())!;

        Assert.Equal(RoadmapState.CoreReady, sqliteState.CurrentState);
        Assert.Equal(RoadmapState.EvidenceBlocked, (await new RoadmapStateStore(repo.Artifacts).LoadAsync())!.CurrentState);
    }

    [Fact]
    public async Task SQLite_decision_ledger_allocates_next_visible_decision_id_after_import()
    {
        using var repo = new TempRepo();
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0001"));
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0002"));
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0003"));
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        var ledger = new SqliteDecisionLedgerStore(repo.Repository);

        Assert.Equal("D0004", await ledger.NextDecisionIdAsync());
        await ledger.AppendAsync(Decision("D0004"));

        Assert.Equal("D0005", await ledger.NextDecisionIdAsync());
        Assert.Equal("D0004", await ledger.LastDecisionIdAsync());
    }

    [Fact]
    public async Task SQLite_lifecycle_save_rejects_duplicate_case_variant_paths()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        var lifecycle = new SqliteArtifactLifecycleStore(repo.Repository);

        RoadmapStepException exception = await Assert.ThrowsAsync<RoadmapStepException>(() =>
            lifecycle.SaveAsync(
            [
                new ArtifactLifecycleEntry(".agents/epic.md", ArtifactLifecycleState.Ready, Instant, "ready"),
                new ArtifactLifecycleEntry(".agents/EPIC.md", ArtifactLifecycleState.Draft, Instant, "draft"),
            ]));

        Assert.Contains("duplicate path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SQLite_split_child_lookup_uses_rows_without_exported_split_files()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await new WorkspaceSqliteStore().ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(RoadmapArtifactPaths.SplitFamilyJson("family-1"));
        var splitFamilies = new SqliteSplitFamilyStore(repo.Repository);

        Assert.True(await splitFamilies.ExistsForChildAsync(".agents/epic-b.md"));
        Assert.Equal(1, await splitFamilies.CountAsync());
    }

    [Fact]
    public async Task SQLite_core_export_imports_into_clean_equivalent_database_for_core_domains()
    {
        using var source = new TempRepo();
        await SeedMigratedWorkspaceAsync(source);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteCoreExportsAsync(source);
        await sqlite.ExportCoreAsync(source.Artifacts);
        using var clean = new TempRepo();
        foreach (string path in CoreExportPaths())
        {
            clean.Write(path, source.Read(path));
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        await cleanSqlite.ImportAsync(clean.Artifacts);

        Assert.Equal(
            CoreSnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            CoreSnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task SQLite_metadata_stores_load_without_exports_and_keep_projection_bodies_on_disk()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], "# Select projection body");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await DeleteMetadataExportsAsync(repo);

        ExecutionPreparationManifest execution = await new SqliteExecutionPreparationManifestStore(repo.Repository).LoadAsync();
        SelectionProvenanceManifest selection = await new SqliteSelectionProvenanceManifestStore(repo.Repository).LoadAsync();
        ProjectionManifest projection = await new SqliteProjectionManifestStore(repo.Repository).LoadAsync();

        Assert.Single(execution.Artifacts);
        Assert.Single(selection.Selections);
        Assert.Single(projection.Entries);
        Assert.False(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.ExecutionPreparationManifest));
        Assert.True(await repo.Artifacts.ExistsAsync(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]));
    }

    [Fact]
    public async Task SQLite_projection_manifest_upsert_replaces_existing_runtime_prompt_metadata()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        var manifest = new SqliteProjectionManifestStore(repo.Repository);

        await manifest.UpsertAsync(ProjectionEntry("SelectNextEpic", "hash-1"));
        await manifest.UpsertAsync(ProjectionEntry("SelectNextEpic", "hash-2"));

        ProjectionManifest loaded = await manifest.LoadAsync();
        ProjectionManifestEntry entry = Assert.Single(loaded.Entries);
        Assert.Equal("hash-2", entry.ProjectionHash);
    }

    [Fact]
    public async Task Malformed_exported_execution_and_selection_manifests_load_empty_during_compatibility_import()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ExecutionPreparationManifest, "{");
        repo.Write(RoadmapArtifactPaths.SelectionProvenanceManifest, "{");

        WorkspaceFilesystemSnapshot snapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(repo.Artifacts);

        Assert.Empty(snapshot.ExecutionPreparationManifest.Artifacts);
        Assert.Empty(snapshot.SelectionProvenanceManifest.Selections);
    }

    [Fact]
    public async Task SQLite_metadata_export_imports_into_clean_equivalent_database()
    {
        using var source = new TempRepo();
        await SeedMigratedWorkspaceAsync(source);
        await SeedMetadataFilesAsync(source);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteMetadataExportsAsync(source);
        await sqlite.ExportMetadataAsync(source.Artifacts);
        using var clean = new TempRepo();
        foreach (string path in MetadataExportPaths())
        {
            clean.Write(path, source.Read(path));
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        await cleanSqlite.ImportAsync(clean.Artifacts);

        Assert.Equal(
            MetadataSnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            MetadataSnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task SQLite_transition_journal_append_preserves_event_order_and_fields()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        var journal = new SqliteTransitionJournalStore(repo.Repository);

        await journal.AppendAsync(JournalRecord("TransitionStarted", "abc", "Started", "None", null, [".agents/output-a.md"]));
        await journal.AppendAsync(JournalRecord("TransitionCompleted", "abc", "Completed", "Select Existing Epic", null, [".agents/output-b.md"]));
        await journal.AppendAsync(JournalRecord("TransitionFailed", "def", "Failed", "None", "boom", [".agents/output-c.md"]));

        IReadOnlyList<TransitionJournalRecord> records = (await new WorkspaceSqliteStore().ReadSnapshotAsync(repo.Repository)).TransitionJournal;
        Assert.Equal(["TransitionStarted", "TransitionCompleted", "TransitionFailed"], records.Select(record => record.Event).ToArray());
        Assert.Equal("abc", records[1].CorrelationId);
        Assert.Equal("Select Existing Epic", records[1].ParserDecision);
        Assert.Equal("boom", records[2].ErrorMessage);
    }

    [Fact]
    public async Task Legacy_jsonl_without_input_snapshot_imports_to_sqlite()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.TransitionJournal, """
            {"event":"TransitionCompleted","correlationId":"legacy","timestamp":"2026-01-02T03:04:05+00:00","previousState":1,"attemptedState":2,"prompt":"SelectNextEpic","projection":"projection","promptContractKey":"contract","inputArtifactHashes":{},"outputPaths":[".agents/selection.md"],"durationMilliseconds":7,"result":"Completed","parserDecision":"Select Existing Epic","errorMessage":null}
            """);
        var sqlite = new WorkspaceSqliteStore();

        await sqlite.ImportAsync(repo.Artifacts);

        TransitionJournalRecord record = Assert.Single((await sqlite.ReadSnapshotAsync(repo.Repository)).TransitionJournal);
        Assert.Equal("legacy", record.CorrelationId);
        Assert.Null(record.InputSnapshot);
    }

    [Fact]
    public async Task SQLite_journal_jsonl_export_imports_to_clean_equivalent_database()
    {
        using var source = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(source.Repository);
        await new SqliteTransitionJournalStore(source.Repository).AppendAsync(
            JournalRecord("TransitionCompleted", "abc", "Completed", "Select Existing Epic", null, [RoadmapArtifactPaths.Selection]));
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ExportJournalAsync(source.Artifacts);
        using var clean = new TempRepo();
        clean.Write(RoadmapArtifactPaths.TransitionJournal, source.Read(RoadmapArtifactPaths.TransitionJournal));

        var cleanSqlite = new WorkspaceSqliteStore();
        await cleanSqlite.ImportAsync(clean.Artifacts);

        Assert.Equal(
            JournalSnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            JournalSnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task SQLite_journal_concurrent_append_smoke_keeps_unique_rows()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        var journal = new SqliteTransitionJournalStore(repo.Repository);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(index =>
            journal.AppendAsync(JournalRecord("TransitionCompleted", $"correlation-{index}", "Completed", "None", null, [$".agents/output-{index}.md"]))));

        IReadOnlyList<TransitionJournalRecord> records = (await new WorkspaceSqliteStore().ReadSnapshotAsync(repo.Repository)).TransitionJournal;
        Assert.Equal(5, records.Count);
        Assert.Equal(5, records.Select(record => record.CorrelationId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Journal_unresolved_output_hook_reports_missing_paths_without_mutating_rows()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        await new SqliteTransitionJournalStore(repo.Repository).AppendAsync(
            JournalRecord("TransitionCompleted", "abc", "Completed", "None", null, [".agents/evidence/execution/missing.0001.md"]));
        var sqlite = new WorkspaceSqliteStore();
        string before = JournalSnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository));

        IReadOnlyList<string> unresolved = await sqlite.FindUnresolvedJournalOutputPathsAsync(repo.Artifacts);

        Assert.Equal([".agents/evidence/execution/missing.0001.md"], unresolved);
        Assert.Equal(before, JournalSnapshotJson(await sqlite.ReadSnapshotAsync(repo.Repository)));
    }

    [Fact]
    public async Task SQLite_loop_history_export_imports_to_clean_equivalent_database()
    {
        using var source = new TempRepo();
        source.Write(OrchestrationArtifactPaths.HistoricalDecision(1), "decision body\r\nwith exact text");
        source.Write(OrchestrationArtifactPaths.HistoricalHandoff(2), "handoff body");
        source.Write(OrchestrationArtifactPaths.HistoricalDelta(3), "delta body\n");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteLoopHistoryExportsAsync(source);
        await sqlite.ExportLoopHistoriesAsync(source.Artifacts);
        using var clean = new TempRepo();
        foreach (string path in LoopHistoryExportPaths())
        {
            clean.Write(path, source.Read(path));
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        await cleanSqlite.ImportAsync(clean.Artifacts);

        Assert.Equal(
            LoopHistorySnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            LoopHistorySnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task Logical_resolver_reads_sqlite_loop_history_after_export_is_deleted()
    {
        using var repo = new TempRepo();
        repo.Write(OrchestrationArtifactPaths.HistoricalHandoff(2), "handoff history");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(OrchestrationArtifactPaths.HistoricalHandoff(2));
        ILogicalArtifactResolver resolver = RoadmapLogicalArtifactServices.CreateResolver(repo.Artifacts);

        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(OrchestrationArtifactPaths.HistoricalHandoff(2));

        Assert.True(result.IsResolved);
        Assert.Equal(LogicalArtifactDomain.LoopHistory, result.Descriptor.Domain);
        Assert.Equal(LogicalArtifactStorageKind.SqliteCanonicalRecord, result.Descriptor.StorageKind);
        Assert.Equal("handoff:.agents/handoffs/handoff.0002.md", result.Descriptor.Identity);
        Assert.Equal("handoff history", result.Content!.Text);
    }

    [Fact]
    public async Task SQLite_execution_evidence_allocates_next_sequence_after_import_without_export()
    {
        using var repo = new TempRepo();
        repo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0003.md", "old evidence");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0003.md");
        var evidence = new SqliteExecutionEvidenceStore(repo.Repository);

        ExecutionEvidenceRecord record = await evidence.WriteAsync("execution-trust-posture", "new evidence");

        Assert.Equal(4, record.Sequence);
        Assert.Equal($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution-trust-posture.0004.md", record.RelativePath);
        Assert.False(await repo.Artifacts.ExistsAsync(record.RelativePath));
    }

    [Fact]
    public async Task RoadmapArtifacts_lists_sqlite_execution_evidence_after_exports_are_deleted()
    {
        using var repo = new TempRepo();
        repo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md", "first");
        repo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0002.md", "second");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md");
        await repo.Artifacts.DeleteAsync($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0002.md");
        var artifacts = new RoadmapArtifacts(repo.Store, repo.Repository, new SqliteExecutionEvidenceStore(repo.Repository));

        IReadOnlyList<string> paths = await artifacts.ListAsync(RoadmapArtifactPaths.ExecutionEvidenceDirectory, "execution.*.md");

        Assert.Equal(
            [
                $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md",
                $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0002.md",
            ],
            paths);
    }

    [Fact]
    public async Task Logical_resolver_reads_sqlite_execution_evidence_after_export_is_deleted()
    {
        using var repo = new TempRepo();
        string evidencePath = $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md";
        repo.Write(evidencePath, "execution evidence");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(evidencePath);
        var artifacts = new RoadmapArtifacts(repo.Store, repo.Repository, new SqliteExecutionEvidenceStore(repo.Repository));
        ILogicalArtifactResolver resolver = RoadmapLogicalArtifactServices.CreateResolver(artifacts);

        LogicalArtifactResolutionResult result = await resolver.ResolveAsync(evidencePath);

        Assert.True(result.IsResolved);
        Assert.Equal(LogicalArtifactDomain.ExecutionEvidence, result.Descriptor.Domain);
        Assert.Equal(LogicalArtifactStorageKind.SqliteCanonicalRecord, result.Descriptor.StorageKind);
        Assert.Equal("execution evidence", result.Content!.Text);
    }

    [Fact]
    public async Task SQLite_execution_evidence_export_imports_to_clean_equivalent_database()
    {
        using var source = new TempRepo();
        source.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md", "body\r\none");
        source.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0002.md", "body two\n");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteExecutionEvidenceExportsAsync(source);
        await sqlite.ExportExecutionEvidenceAsync(source.Artifacts);
        using var clean = new TempRepo();
        foreach (string path in ExecutionEvidenceExportPaths())
        {
            clean.Write(path, source.Read(path));
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        await cleanSqlite.ImportAsync(clean.Artifacts);

        Assert.Equal(
            ExecutionEvidenceSnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            ExecutionEvidenceSnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task Workspace_sync_full_export_regenerates_all_migrated_files()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await DeleteFullExportSurfaceAsync(repo);

        WorkspaceSqliteOperationResult result = await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);

        Assert.Equal(WorkspaceStorageResultCategory.Exported, result.Category);
        Assert.Contains("Rows:", result.Message, StringComparison.Ordinal);
        Assert.Contains("Files:", result.Message, StringComparison.Ordinal);
        foreach (string path in FullExportPaths())
        {
            Assert.True(await repo.Artifacts.ExistsAsync(path), $"Expected exported path: {path}");
        }
    }

    [Fact]
    public async Task Workspace_sync_generated_export_imports_to_clean_equivalent_database()
    {
        using var source = new TempRepo();
        await SeedMigratedWorkspaceAsync(source);
        await SeedMetadataFilesAsync(source);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteFullExportSurfaceAsync(source);
        await new WorkspaceSyncService(sqlite).ExportAsync(source.Artifacts);
        using var clean = new TempRepo();
        foreach (string path in FullExportPaths())
        {
            clean.Write(path, source.Read(path));
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        WorkspaceSqliteOperationResult result = await new WorkspaceSyncService(cleanSqlite).ImportAsync(clean.Artifacts);

        Assert.Equal(WorkspaceStorageResultCategory.Imported, result.Category);
        Assert.Contains("Rows:", result.Message, StringComparison.Ordinal);
        Assert.Contains("Files:", result.Message, StringComparison.Ordinal);
        Assert.Equal(
            SnapshotJson(await sqlite.ReadSnapshotAsync(source.Repository)),
            SnapshotJson(await cleanSqlite.ReadSnapshotAsync(clean.Repository)));
    }

    [Fact]
    public async Task Workspace_sync_export_records_markers_for_all_domains()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await DeleteFullExportSurfaceAsync(repo);
        var sync = new WorkspaceSyncService(sqlite);

        await sync.ExportAsync(repo.Artifacts);

        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers =
            await sqlite.ReadSyncMarkersAsync(repo.Repository);
        Assert.Empty(WorkspaceSyncDomains.All.Except(markers.Keys));
        Assert.Empty(markers.Keys.Except(WorkspaceSyncDomains.All));
        Assert.All(markers.Values, marker =>
        {
            Assert.False(string.IsNullOrWhiteSpace(marker.CanonicalHash));
            Assert.False(string.IsNullOrWhiteSpace(marker.ExportHash));
            Assert.Equal(1, marker.Generation);
        });
    }

    [Fact]
    public async Task Workspace_sync_scoped_export_leaves_unrelated_domains_unchanged()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await DeleteFullExportSurfaceAsync(repo);

        WorkspaceSqliteOperationResult result = await new WorkspaceSyncService(sqlite).ExportAsync(
            repo.Artifacts,
            new WorkspaceSyncOptions(new HashSet<WorkspaceSyncDomain> { WorkspaceSyncDomain.Core }));

        Assert.Equal(WorkspaceStorageResultCategory.Exported, result.Category);
        foreach (string path in CoreExportPaths())
        {
            Assert.True(await repo.Artifacts.ExistsAsync(path), $"Expected scoped core export: {path}");
        }

        foreach (string path in MetadataExportPaths())
        {
            Assert.False(await repo.Artifacts.ExistsAsync(path), $"Metadata export should be untouched: {path}");
        }
    }

    [Fact]
    public async Task Workspace_sync_scoped_export_requires_domains_referenced_by_journal_outputs()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new SqliteTransitionJournalStore(repo.Repository).AppendAsync(JournalRecord(
            "TransitionCompleted",
            "correlation-evidence",
            "Completed",
            "Execution evidence captured",
            null,
            [$"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0004.md"]));
        var sync = new WorkspaceSyncService(sqlite);

        WorkspaceSqliteOperationResult blocked = await sync.ExportAsync(
            repo.Artifacts,
            new WorkspaceSyncOptions(new HashSet<WorkspaceSyncDomain> { WorkspaceSyncDomain.Journal }));

        Assert.Equal(WorkspaceStorageResultCategory.ValidationFailure, blocked.Category);
        Assert.Contains("Journal ->", blocked.Message, StringComparison.Ordinal);
        Assert.Contains("ExecutionEvidence", blocked.Message, StringComparison.Ordinal);

        WorkspaceSqliteOperationResult exported = await sync.ExportAsync(
            repo.Artifacts,
            new WorkspaceSyncOptions(
                new HashSet<WorkspaceSyncDomain>
                {
                    WorkspaceSyncDomain.Journal,
                    WorkspaceSyncDomain.LoopHistories,
                    WorkspaceSyncDomain.ExecutionEvidence,
                }));

        Assert.Equal(WorkspaceStorageResultCategory.Exported, exported.Category);
    }

    [Fact]
    [Trait("Baseline", "KnownRisk")]
    public async Task Workspace_sync_import_blocks_stale_export_when_database_changed()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        var sync = new WorkspaceSyncService(sqlite);
        await sync.ImportAsync(repo.Artifacts);
        await new SqliteRoadmapStateStore(repo.Repository).SaveAsync(State(RoadmapState.EvidenceBlocked));

        WorkspaceSqliteOperationResult result = await sync.ImportAsync(repo.Artifacts);

        Assert.Equal(WorkspaceStorageResultCategory.StaleExport, result.Category);
        Assert.Contains("stale", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RoadmapState.EvidenceBlocked, (await new SqliteRoadmapStateStore(repo.Repository).LoadAsync())!.CurrentState);
    }

    [Fact]
    [Trait("Baseline", "KnownRisk")]
    public async Task Workspace_sync_import_reports_conflict_when_database_and_export_changed()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        var sync = new WorkspaceSyncService(sqlite);
        await sync.ImportAsync(repo.Artifacts);
        await new SqliteRoadmapStateStore(repo.Repository).SaveAsync(State(RoadmapState.EvidenceBlocked));
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(RoadmapState.ActiveEpicReady));

        WorkspaceSqliteOperationResult result = await sync.ImportAsync(repo.Artifacts);

        Assert.Equal(WorkspaceStorageResultCategory.Conflict, result.Category);
        Assert.Contains("both changed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RoadmapState.EvidenceBlocked, (await new SqliteRoadmapStateStore(repo.Repository).LoadAsync())!.CurrentState);
    }

    [Fact]
    public async Task Workspace_sync_export_import_export_is_stable()
    {
        using var source = new TempRepo();
        await SeedMigratedWorkspaceAsync(source);
        await SeedMetadataFilesAsync(source);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(source.Artifacts);
        await DeleteFullExportSurfaceAsync(source);
        await new WorkspaceSyncService(sqlite).ExportAsync(source.Artifacts);
        Dictionary<string, string> firstExport = FullExportPaths().ToDictionary(path => path, source.Read);

        using var clean = new TempRepo();
        foreach ((string path, string content) in firstExport)
        {
            clean.Write(path, content);
        }

        var cleanSqlite = new WorkspaceSqliteStore();
        var cleanSync = new WorkspaceSyncService(cleanSqlite);
        await cleanSync.ImportAsync(clean.Artifacts);
        await DeleteFullExportSurfaceAsync(clean);
        await cleanSync.ExportAsync(clean.Artifacts, new WorkspaceSyncOptions(ForceExport: true));

        foreach ((string path, string content) in firstExport)
        {
            Assert.Equal(content, clean.Read(path));
        }
    }

    [Fact]
    public async Task Workspace_verify_valid_sqlite_workspace_with_fresh_exports_succeeds()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Findings.Select(finding => finding.ToString())));
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Workspace_verify_orphaned_execution_evidence_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        await new SqliteExecutionEvidenceStore(repo.Repository).WriteAsync("orphan", "orphaned evidence");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.OrphanedArtifact &&
            finding.Identity == $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/orphan.0001.md");
    }

    [Fact]
    public async Task Workspace_verify_invalid_split_child_reference_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(".agents/epic-b.md");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.InvalidReference &&
            finding.Rule == "split-child-reference" &&
            finding.Identity == ".agents/epic-b.md");
    }

    [Fact]
    public async Task Workspace_verify_invalid_lifecycle_path_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        await new SqliteArtifactLifecycleStore(repo.Repository).SaveAsync(
        [
            new ArtifactLifecycleEntry(".agents/missing-lifecycle.md", ArtifactLifecycleState.Ready, Instant, "missing"),
        ]);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.InvalidReference &&
            finding.Rule == "lifecycle-path-reference" &&
            finding.Identity == ".agents/missing-lifecycle.md");
    }

    [Fact]
    public async Task Workspace_verify_duplicate_migrated_identity_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(
            databasePath,
            $$"""
            INSERT INTO execution_evidence (logical_path, stem, sequence, body, content_hash, created_at, writer, metadata_json)
            VALUES (
                '{{OrchestrationArtifactPaths.HistoricalDecision(1)}}',
                'duplicate',
                1,
                'duplicate body',
                '{{WorkspaceSqliteStore.Sha256("duplicate body")}}',
                '2026-01-01T00:00:00.0000000+00:00',
                NULL,
                '{}');
            """);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.DuplicateIdentity &&
            finding.Identity == OrchestrationArtifactPaths.HistoricalDecision(1));
    }

    [Fact]
    public async Task Workspace_verify_legacy_filesystem_workspace_succeeds_without_creating_database()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService().VerifyAsync(
            repo.Artifacts,
            new WorkspaceVerificationOptions(FullRoundtrip: true));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Findings.Select(finding => finding.ToString())));
        Assert.False(File.Exists(databasePath));
    }

    [Fact]
    public async Task Workspace_verify_stale_export_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        var sync = new WorkspaceSyncService(sqlite);
        await sync.ImportAsync(repo.Artifacts);
        await sync.ExportAsync(repo.Artifacts);
        await new SqliteRoadmapStateStore(repo.Repository).SaveAsync(State(RoadmapState.EvidenceBlocked));

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding => finding.Kind == WorkspaceVerificationFindingKind.StaleExport);
    }

    [Fact]
    public async Task Workspace_verify_missing_export_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        var sync = new WorkspaceSyncService(sqlite);
        await sync.ImportAsync(repo.Artifacts);
        await sync.ExportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(RoadmapArtifactPaths.DecisionLedgerJson);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.MissingExport &&
            finding.Identity == RoadmapArtifactPaths.DecisionLedgerJson);
    }

    [Fact]
    public async Task Workspace_verify_unresolved_journal_output_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.UnresolvedPath &&
            finding.Identity == RoadmapArtifactPaths.Selection);
    }

    [Fact]
    public async Task Workspace_verify_divergent_database_and_export_reports_conflict()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        var sqlite = new WorkspaceSqliteStore();
        var sync = new WorkspaceSyncService(sqlite);
        await sync.ImportAsync(repo.Artifacts);
        await sync.ExportAsync(repo.Artifacts);
        await new SqliteRoadmapStateStore(repo.Repository).SaveAsync(State(RoadmapState.EvidenceBlocked));
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(RoadmapState.ActiveEpicReady));

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding => finding.Kind == WorkspaceVerificationFindingKind.Conflict);
    }

    [Fact]
    public async Task Workspace_verify_broken_archive_metadata_fails()
    {
        using var repo = new TempRepo();
        await new WorkspaceSqliteStore().InitializeAsync(repo.Repository);
        repo.Write(".agents/archive/epics/1/archive-metadata.json", "{");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService().VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding => finding.Kind == WorkspaceVerificationFindingKind.UnrecoverableArchive);
    }

    [Fact]
    public async Task Workspace_verify_corrupt_database_row_fails()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(
            databasePath,
            """
            INSERT INTO execution_evidence (logical_path, stem, sequence, body, content_hash, created_at, writer, metadata_json)
            VALUES ('.agents/evidence/execution/execution.0001.md', 'execution', 1, 'body', 'wrong-hash', '2026-01-01T00:00:00.0000000+00:00', NULL, '{}');
            """);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding => finding.Kind == WorkspaceVerificationFindingKind.CorruptDomain);
    }

    [Fact]
    public async Task Workspace_verify_valid_runtime_persistence_rows_succeeds()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        const string telemetryJson = """{"repoName":"repo","sessionType":"Decision"}""";
        await ExecuteSqlAsync(
            databasePath,
            $$"""
            INSERT INTO decision_session_resume (id, document_json, saved_at)
            VALUES (1, '{"schemaVersion":1,"threadId":"thread-1"}', '2026-01-01T00:00:00.0000000+00:00');

            INSERT INTO session_telemetry_events (
                recorded_at, repo_name, session_id, session_type, turn_index, document_json, content_hash)
            VALUES (
                '2026-01-01T00:00:00.0000000+00:00',
                'repo',
                'sid',
                'Decision',
                1,
                '{{telemetryJson}}',
                '{{WorkspaceSqliteStore.Sha256(telemetryJson)}}');
            """);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Findings.Select(finding => finding.ToString())));
    }

    [Fact]
    public async Task Workspace_verify_corrupt_runtime_telemetry_hash_fails()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(
            databasePath,
            """
            INSERT INTO session_telemetry_events (
                recorded_at, repo_name, session_id, session_type, turn_index, document_json, content_hash)
            VALUES (
                '2026-01-01T00:00:00.0000000+00:00',
                'repo',
                'sid',
                'Decision',
                1,
                '{"repoName":"repo"}',
                'wrong-hash');
            """);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.CorruptDomain &&
            finding.Domain == "runtime-telemetry" &&
            finding.Rule == "content-hash");
    }

    [Fact]
    public async Task Workspace_verify_legacy_resume_file_conflicts_with_canonical_resume()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(
            databasePath,
            """
            INSERT INTO decision_session_resume (id, document_json, saved_at)
            VALUES (1, '{"schemaVersion":1,"threadId":"thread-1"}', '2026-01-01T00:00:00.0000000+00:00');
            """);
        string legacyPath = Path.Combine(repo.Root, ".LoopRelay", "decision-session.json");
        await File.WriteAllTextAsync(legacyPath, """{"schemaVersion":1,"threadId":"legacy"}""");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.Conflict &&
            finding.Domain == "runtime-decision-resume" &&
            finding.Rule == "legacy-file-authority");
    }

    [Fact]
    public async Task Workspace_verify_unsupported_schema_fails()
    {
        using var repo = new TempRepo();
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.InitializeAsync(repo.Repository);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        await ExecuteSqlAsync(databasePath, "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';");

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(repo.Artifacts);

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding => finding.Kind == WorkspaceVerificationFindingKind.UnsupportedVersion);
    }

    [Fact]
    public async Task Workspace_verify_full_roundtrip_does_not_mutate_database_or_exports()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        string databasePath = WorkspaceDatabaseLocator.Resolve(repo.Repository);
        string beforeDatabase = FileHash(databasePath);
        Dictionary<string, string> beforeExports = FullExportPaths().ToDictionary(path => path, repo.Read);

        WorkspaceVerificationResult result = await new WorkspaceVerificationService(sqlite).VerifyAsync(
            repo.Artifacts,
            new WorkspaceVerificationOptions(FullRoundtrip: true));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Findings.Select(finding => finding.ToString())));
        Assert.Equal(beforeDatabase, FileHash(databasePath));
        foreach ((string path, string content) in beforeExports)
        {
            Assert.Equal(content, repo.Read(path));
        }
    }

    [Fact]
    public async Task Workspace_verify_nondeterministic_roundtrip_fails()
    {
        using var repo = new TempRepo();
        await SeedMigratedWorkspaceAsync(repo);
        await SeedMetadataFilesAsync(repo);
        repo.Write(RoadmapArtifactPaths.Selection, "# Selection");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await new WorkspaceSyncService(sqlite).ExportAsync(repo.Artifacts);
        var verifier = new WorkspaceVerificationService(
            sqlite,
            snapshot => snapshot with { TransitionJournal = [] });

        WorkspaceVerificationResult result = await verifier.VerifyAsync(
            repo.Artifacts,
            new WorkspaceVerificationOptions(FullRoundtrip: true));

        Assert.False(result.Success);
        Assert.Contains(result.Findings, finding =>
            finding.Kind == WorkspaceVerificationFindingKind.NondeterministicRoundTrip &&
            finding.Domain == WorkspaceSyncDomain.Journal.ToString());
    }

    private static async Task SeedMigratedWorkspaceAsync(TempRepo repo)
    {
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Active Epic", "EPIC-ACTIVE"));
        repo.Write(".agents/epic-a.md", RoadmapSamples.ValidEpic("Child A", "EPIC-A"));
        repo.Write(".agents/epic-b.md", RoadmapSamples.ValidEpic("Child B", "EPIC-B"));
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0001"));
        await new RoadmapStateStore(repo.Artifacts).SaveAsync(State(RoadmapState.CoreReady));
        await new ArtifactLifecycleStore(repo.Artifacts).SaveAsync(
        [
            new ArtifactLifecycleEntry(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready, Instant, "ready"),
        ]);
        await new SplitFamilyStore(repo.Artifacts).WriteAsync(new SplitFamily(
            "family-1",
            "Split proposal",
            [".agents/epic-a.md", ".agents/epic-b.md"],
            [".agents/epic-a.md", ".agents/epic-b.md"],
            ".agents/epic-a.md",
            "Best child",
            Instant));
        await new ProjectionManifestStore(repo.Artifacts).SaveAsync(new ProjectionManifest(
        [
            ProjectionEntry("SelectNextEpic", "projection-hash"),
        ]));
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
            [
                RoadmapArtifactPaths.Selection,
                OrchestrationArtifactPaths.HistoricalDecision(1),
                OrchestrationArtifactPaths.HistoricalHandoff(2),
                OrchestrationArtifactPaths.HistoricalDelta(3),
                $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0004.md",
            ],
            42,
            "Completed",
            "Select Existing Epic",
            null));
        repo.Write(OrchestrationArtifactPaths.HistoricalDecision(1), "decision history");
        repo.Write(OrchestrationArtifactPaths.HistoricalHandoff(2), "handoff history");
        repo.Write(OrchestrationArtifactPaths.HistoricalDelta(3), "delta history");
        repo.Write($"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0004.md", "execution evidence");
    }

    private static RoadmapStateDocument State(RoadmapState currentState) =>
        new(
            currentState,
            [new ArtifactStateRow("Epic", RoadmapArtifactPaths.ActiveEpic, "Ready")],
            new RoadmapTransitionSummary(
                RoadmapState.CoreReady,
                currentState,
                "SelectNextEpic",
                RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
                RoadmapArtifactPaths.Selection,
                "D0001",
                TransitionStatus.Completed,
                Instant,
                Instant),
            [],
            "D0001",
            0,
            1,
            new ProjectionManifestCounts(1, 0, 0),
            RoadmapTransitionIntent.Empty(currentState),
            [currentState.ToString()],
            []);

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

    private static TransitionJournalRecord JournalRecord(
        string @event,
        string correlationId,
        string result,
        string decision,
        string? error,
        IReadOnlyList<string> outputs) =>
        new(
            @event,
            correlationId,
            Instant,
            RoadmapState.CoreReady,
            RoadmapState.SelectNextStrategicInitiative,
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            "contract",
            new Dictionary<string, string>(StringComparer.Ordinal),
            outputs,
            7,
            result,
            decision,
            error);

    private static async Task SeedMetadataFilesAsync(TempRepo repo)
    {
        await new ExecutionPreparationManifestStore(repo.Artifacts).SaveAsync(new ExecutionPreparationManifest(
            ExecutionPreparationManifest.CurrentSchemaVersion,
            RoadmapArtifactPaths.ActiveEpic,
            "active-hash",
            [new ExecutionPreparationManifestInput("MilestoneSpec", ".agents/specs/s1.md", "spec-hash")],
            [DerivedEntry("ExecutionPrompt", RoadmapArtifactPaths.ExecutionPrompt)]));
        await new SelectionProvenanceManifestStore(repo.Artifacts).SaveAsync(
            SelectionProvenanceManifest.Empty.UpsertActive(DerivedEntry("Selection", RoadmapArtifactPaths.Selection)));
    }

    private static ProjectionManifestEntry ProjectionEntry(string runtimePromptName, string projectionHash) =>
        new(
            runtimePromptName,
            "ProjectionForSelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            "prompt-source-hash",
            RoadmapArtifactPaths.ProjectContextSourceFiles,
            "context-hash",
            projectionHash,
            Instant,
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Fresh,
            null,
            ProjectionProvenanceStatus.Trusted,
            runtimePromptName,
            "Selection",
            [],
            []);

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

    private static string SnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    private static string CoreSnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(
            new
            {
                snapshot.DecisionLedger,
                snapshot.RoadmapState,
                snapshot.ArtifactLifecycle,
                snapshot.SplitFamilies,
            },
            SnapshotJsonOptions);

    private static string MetadataSnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(
            new
            {
                snapshot.ExecutionPreparationManifest,
                snapshot.SelectionProvenanceManifest,
                snapshot.ProjectionManifest,
            },
            SnapshotJsonOptions);

    private static string JournalSnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot.TransitionJournal, SnapshotJsonOptions);

    private static string LoopHistorySnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot.LoopHistories, SnapshotJsonOptions);

    private static string ExecutionEvidenceSnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot.ExecutionEvidence, SnapshotJsonOptions);

    private static async Task DeleteCoreExportsAsync(TempRepo repo)
    {
        foreach (string path in CoreExportPaths())
        {
            await repo.Artifacts.DeleteAsync(path);
        }
    }

    private static IReadOnlyList<string> CoreExportPaths() =>
    [
        RoadmapArtifactPaths.DecisionLedgerJson,
        RoadmapArtifactPaths.StateJson,
        RoadmapArtifactPaths.LifecycleJson,
        RoadmapArtifactPaths.SplitFamilyJson("family-1"),
    ];

    private static async Task DeleteMetadataExportsAsync(TempRepo repo)
    {
        foreach (string path in MetadataExportPaths())
        {
            await repo.Artifacts.DeleteAsync(path);
        }
    }

    private static IReadOnlyList<string> MetadataExportPaths() =>
    [
        RoadmapArtifactPaths.ExecutionPreparationManifest,
        RoadmapArtifactPaths.SelectionProvenanceManifest,
        RoadmapArtifactPaths.ProjectionsManifestJson,
    ];

    private static async Task DeleteLoopHistoryExportsAsync(TempRepo repo)
    {
        foreach (string path in LoopHistoryExportPaths())
        {
            await repo.Artifacts.DeleteAsync(path);
        }
    }

    private static IReadOnlyList<string> LoopHistoryExportPaths() =>
    [
        OrchestrationArtifactPaths.HistoricalDecision(1),
        OrchestrationArtifactPaths.HistoricalHandoff(2),
        OrchestrationArtifactPaths.HistoricalDelta(3),
    ];

    private static async Task DeleteExecutionEvidenceExportsAsync(TempRepo repo)
    {
        foreach (string path in ExecutionEvidenceExportPaths())
        {
            await repo.Artifacts.DeleteAsync(path);
        }
    }

    private static IReadOnlyList<string> ExecutionEvidenceExportPaths() =>
    [
        $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0001.md",
        $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0002.md",
    ];

    private static async Task DeleteFullExportSurfaceAsync(TempRepo repo)
    {
        foreach (string path in FullExportPaths())
        {
            await repo.Artifacts.DeleteAsync(path);
        }
    }

    private static IReadOnlyList<string> FullExportPaths() =>
    [
        ..CoreExportPaths(),
        ..MetadataExportPaths(),
        RoadmapArtifactPaths.TransitionJournal,
        OrchestrationArtifactPaths.HistoricalDecision(1),
        OrchestrationArtifactPaths.HistoricalHandoff(2),
        OrchestrationArtifactPaths.HistoricalDelta(3),
        $"{RoadmapArtifactPaths.ExecutionEvidenceDirectory}/execution.0004.md",
    ];

    private static async Task ExecuteSqlAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString(databasePath));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarStringAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString(databasePath));
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync())!;
    }

    private static string ConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString();

    private static string FileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

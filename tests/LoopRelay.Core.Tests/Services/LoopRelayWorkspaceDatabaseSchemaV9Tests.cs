using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

public sealed class LoopRelayWorkspaceDatabaseSchemaV9Tests
{
    private static readonly string[] SpineTables =
    [
        "workspace_identity",
        "runs",
        "workflow_instances",
        "attempts",
        "agent_sessions",
        "agent_turns",
    ];

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_StampsVersionNineAndCreatesSpineTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(9, LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
        Assert.Equal("9", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal(LoopRelayWorkspaceDatabase.SchemaIdentity, await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_identity';"));
        Assert.Equal(LoopRelayWorkspaceDatabase.SchemaFamily, await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_family';"));
        Assert.Equal(
            LoopRelayWorkspaceDatabase.CanonicalV9ShapeFingerprint,
            await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_shape';"));
        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaFamily.CanonicalWorkspace, inspection.Family);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV9Complete, inspection.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV9ShapeFingerprint, inspection.ShapeFingerprint);
        foreach (string table in SpineTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected spine table `{table}` to exist.");
        }

        string? workspaceId = await ScalarStringAsync(connection, "SELECT workspace_id FROM workspace_identity WHERE id = 1;");
        Assert.NotNull(workspaceId);
        Assert.StartsWith("ws_", workspaceId, StringComparison.Ordinal);
        Assert.Equal(29, workspaceId.Length);
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesWarningsTableAndNoLegacyLatchTable()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(await TableExistsAsync(connection, "evaluation_warnings"), "Expected table `evaluation_warnings` to exist.");
        Assert.False(await TableExistsAsync(connection, "canonical_blockers"), "Expected table `canonical_blockers` to be dropped.");
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesV9HistoryImportAndProjectionTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        foreach (string table in (string[])
        [
            "history_evidence_sets",
            "history_evidence_items",
            "compatibility_import_operations",
            "compatibility_import_events",
            "canonical_projection_effects",
            "transition_recovery_plans",
            "canonical_effect_intents",
            "persistence_projection_checkpoints",
            "workspace_schema_migrations",
            "workspace_schema_convergences",
            "workspace_identity_metadata",
        ])
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected v9 table `{table}` to exist.");
        }
    }

    [Fact]
    public async Task InspectSchemaAsync_ClassifiesBranchLocalContinuityV3WithoutTrustingItsVersionNumber()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '3');
            CREATE TABLE session_continuity_profiles(profile_digest text primary key);
            CREATE TABLE decision_session_scopes(scope_id text primary key);
            """);

        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);

        Assert.Equal(WorkspaceSchemaFamily.LegacyContinuity, inspection.Family);
        await Assert.ThrowsAsync<WorkspaceCompatibilityImportRequiredException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
    }

    [Fact]
    public async Task LegacyContinuityImporter_LeavesSourceUntouchedAndCreatesJournaledV9Shadow()
    {
        Repository repository = CreateRepository();
        string sourcePath = CreateDatabasePath(repository);
        string targetPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "import-shadow.sqlite3");
        await using (SqliteConnection source = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(sourcePath))
        {
            await source.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(source);
            await ExecuteAsync(
                source,
                """
                DELETE FROM schema_metadata WHERE key IN ('schema_identity', 'schema_family');
                UPDATE schema_metadata SET value = '3' WHERE key = 'schema_version';
                DROP TABLE workspace_identity;
                """);
        }

        WorkspaceCompatibilityImportResult result =
            await LegacyContinuityWorkspaceImporter.ImportToShadowAsync(sourcePath, targetPath);

        Assert.Equal(WorkspaceSchemaFamily.LegacyContinuity, result.SourceSchema.Family);
        Assert.Equal(9, result.TargetSchemaVersion);
        await using (SqliteConnection source = LoopRelayWorkspaceDatabase.OpenReadOnly(sourcePath))
        {
            await source.OpenAsync();
            Assert.Equal(WorkspaceSchemaFamily.LegacyContinuity,
                (await LoopRelayWorkspaceDatabase.InspectSchemaAsync(source)).Family);
        }

        await using SqliteConnection shadow = LoopRelayWorkspaceDatabase.OpenReadOnly(targetPath);
        await shadow.OpenAsync();
        WorkspaceSchemaInspection targetInspection =
            await LoopRelayWorkspaceDatabase.InspectSchemaAsync(shadow);
        Assert.Equal(WorkspaceSchemaFamily.CanonicalWorkspace, targetInspection.Family);
        Assert.Equal(9, targetInspection.Version);
        Assert.Equal(4L, await ScalarLongAsync(
            shadow,
            $"SELECT COUNT(*) FROM compatibility_import_events WHERE import_id = '{result.ImportId}';"));
        Assert.False(await TableExistsAsync(shadow, "compatibility_import_bootstrap"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_PreservesLegacyWorkspaceIdentityVerbatim()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        const string legacyIdentity = "0123456789abcdef0123456789abcdef";
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            $"""
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            CREATE TABLE workspace_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '8');
            INSERT INTO workspace_metadata (key, value) VALUES ('workspace_id', '{legacyIdentity}');
            """);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(legacyIdentity, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
        Assert.Equal("legacy-opaque", await ScalarStringAsync(
            connection,
            "SELECT identity_format FROM workspace_identity_metadata WHERE id = 1;"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesReadReceiptsTableWithExpectedColumns()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(await TableExistsAsync(connection, "read_receipts"), "Expected table `read_receipts` to exist.");
        IReadOnlyList<string> columns = await TableColumnsAsync(connection, "read_receipts");
        string[] expectedColumns =
        [
            "receipt_id",
            "run_id",
            "workflow_identity",
            "transition_identity",
            "attempt_id",
            "commit_hash",
            "input_surfaces_json",
            "surface_tree_hashes_json",
            "files_json",
            "products_json",
            "validation",
            "consumed_at",
            "transition_run_id",
        ];
        Assert.Equal(expectedColumns, columns);
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesChainBoundaryEventsAndRetiresChainRunsTable()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(await TableExistsAsync(connection, "canonical_chain_boundary_events"), "Expected table `canonical_chain_boundary_events` to exist.");
        Assert.False(await TableExistsAsync(connection, "canonical_workflow_chain_runs"), "Expected the never-written `canonical_workflow_chain_runs` table to be absent in new databases.");
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesLineageColumnsOnHistoryFactTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        IReadOnlyList<string> loopHistoryColumns = await TableColumnsAsync(connection, "loop_history");
        Assert.Contains("history_id", loopHistoryColumns);
        Assert.Contains("run_id", loopHistoryColumns);
        Assert.Contains("transition_run_id", loopHistoryColumns);
        Assert.Contains("attempt_id", loopHistoryColumns);
        Assert.Contains("transition_run_id", await TableColumnsAsync(connection, "evaluation_warnings"));
        Assert.Contains("transition_run_id", await TableColumnsAsync(connection, "canonical_gate_evaluations"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesPolicyResolutionsTableAndAttemptPolicyColumn()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(await TableExistsAsync(connection, "canonical_policy_resolutions"), "Expected table `canonical_policy_resolutions` to exist.");
        string[] expectedColumns =
        [
            "resolution_id",
            "policy_id",
            "schema_version",
            "resolved_json",
            "provenance_json",
            "source_description",
            "recorded_at",
        ];
        Assert.Equal(expectedColumns, await TableColumnsAsync(connection, "canonical_policy_resolutions"));
        Assert.Contains("policy_id", await TableColumnsAsync(connection, "attempts"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesRenderedPromptsTableAndSessionProfileColumns()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.True(await TableExistsAsync(connection, "canonical_rendered_prompts"), "Expected table `canonical_rendered_prompts` to exist.");
        Assert.True(await TableExistsAsync(connection, "prompt_dispatch_events"), "Expected table `prompt_dispatch_events` to exist.");
        Assert.True(await TableExistsAsync(connection, "execution_recommendation_evidence"));
        Assert.True(await TableExistsAsync(connection, "runtime_profile_evaluations"));
        string[] expectedColumns =
        [
            "rendered_prompt_id",
            "transition_run_id",
            "attempt_id",
            "session_id",
            "turn_id",
            "prompt_identity",
            "template_source_hash",
            "rendered_sha256",
            "rendered_text",
            "consumed_inputs_json",
            "policy_id",
            "rendered_at",
            "persistence_id",
            "prompt_policy_profile_id",
            "consumed_input_manifest_id",
            "rendered_encoding",
        ];
        Assert.Equal(expectedColumns, await TableColumnsAsync(connection, "canonical_rendered_prompts"));
        Assert.Contains("session_id", await TableColumnsAsync(connection, "prompt_dispatch_events"));
        Assert.Contains("turn_id", await TableColumnsAsync(connection, "prompt_dispatch_events"));
        Assert.Contains("effort", await TableColumnsAsync(connection, "agent_sessions"));
        Assert.Contains("sandbox", await TableColumnsAsync(connection, "agent_sessions"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesTurnEvidenceColumnsAndRuntimePrerequisitesTable()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        string[] expectedTurnColumns =
        [
            "turn_id",
            "session_id",
            "turn_index",
            "recorded_at",
            "state",
            "prompt_sha256",
            "prompt_tokens",
            "output_tokens",
            "cached_input_tokens",
            "diagnostics_kind",
            "diagnostics",
        ];
        Assert.Equal(expectedTurnColumns, await TableColumnsAsync(connection, "agent_turns"));

        Assert.True(
            await TableExistsAsync(connection, "canonical_runtime_prerequisites"),
            "Expected table `canonical_runtime_prerequisites` to exist.");
        string[] expectedPrerequisiteColumns =
        [
            "prerequisite_check_id",
            "run_id",
            "checked_at",
            "diagnostics_json",
        ];
        Assert.Equal(expectedPrerequisiteColumns, await TableColumnsAsync(connection, "canonical_runtime_prerequisites"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_ConvergesMerge4PartialV9WithoutRecordingFakeNineToNineMigration()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);

        await RemoveIncomingV9FeaturesAsync(connection);
        await ExecuteAsync(
            connection,
            "DELETE FROM schema_metadata WHERE key = 'schema_shape'; DROP TABLE workspace_schema_convergences;");

        WorkspaceSchemaInspection provisional = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.Merge4V9Partial, provisional.Shape);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        WorkspaceSchemaInspection complete = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV9Complete, complete.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV9ShapeFingerprint, complete.ShapeFingerprint);
        Assert.Equal(workspaceId, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
        Assert.Equal(0L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM workspace_schema_migrations WHERE from_version = 9 AND to_version = 9;"));
        Assert.Equal("Merge4V9Partial", await ScalarStringAsync(
            connection,
            "SELECT source_shape FROM workspace_schema_convergences;"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_ConvergesArchitecturePartialV9AndPreservesWorkspaceIdentity()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);

        await RemoveMerge4V9FeaturesAsync(connection);
        await ExecuteAsync(
            connection,
            "DELETE FROM schema_metadata WHERE key IN ('schema_identity', 'schema_family', 'schema_shape');");

        WorkspaceSchemaInspection provisional = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.False(provisional.HasExplicitLineage);
        Assert.Equal(WorkspaceSchemaShape.ArchitectureConvergenceV9Partial, provisional.Shape);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        WorkspaceSchemaInspection complete = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV9Complete, complete.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV9ShapeFingerprint, complete.ShapeFingerprint);
        Assert.Equal(workspaceId, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
        Assert.Equal("ArchitectureConvergenceV9Partial", await ScalarStringAsync(
            connection,
            "SELECT source_shape FROM workspace_schema_convergences;"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_ConvergesRecognizedMixedV9BeforeStampingCompleteShape()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);
        await ExecuteAsync(
            connection,
            """
            DELETE FROM schema_metadata WHERE key IN ('schema_identity', 'schema_family', 'schema_shape');
            DROP TABLE workspace_schema_convergences;
            """);

        WorkspaceSchemaInspection provisional = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.RecognizedMixedV9Partial, provisional.Shape);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        WorkspaceSchemaInspection complete = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV9Complete, complete.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV9ShapeFingerprint, complete.ShapeFingerprint);
        Assert.Equal(workspaceId, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
    }

    [Fact]
    public async Task EnsureSchemaAsync_RejectsUnknownOrContradictoryV9ShapesWithoutMutation()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '9');
            CREATE TABLE unrelated_state(id text primary key);
            """);

        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.UnknownV9Shape, inspection.Shape);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
        Assert.False(await TableExistsAsync(connection, "workspace_identity"));
        Assert.Null(await ScalarStringAsync(
            connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_shape';"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_RejectsStampedCanonicalV9WhenItsPhysicalShapeIsCorrupt()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await ExecuteAsync(connection, "DROP TABLE canonical_runtime_prerequisites;");

        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CorruptCanonicalV9, inspection.Shape);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
    }

    [Fact]
    public async Task EnsureSchemaAsync_RollsBackAllConvergenceWorkWhenFinalShapeStampFails()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '8');
            CREATE TRIGGER reject_shape_stamp BEFORE INSERT ON schema_metadata
            WHEN NEW.key = 'schema_shape'
            BEGIN
                SELECT RAISE(ABORT, 'shape stamp rejected');
            END;
            """);

        await Assert.ThrowsAsync<SqliteException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));

        Assert.Equal("8", await ScalarStringAsync(
            connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.False(await TableExistsAsync(connection, "workspace_schema_convergences"));
        Assert.False(await TableExistsAsync(connection, "workspace_identity"));
        Assert.Null(await ScalarStringAsync(
            connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_shape';"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsTurnEvidenceColumnsToVersionEightTurnsWithoutRewritingFactRows()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '8');

                CREATE TABLE agent_turns(
                    turn_id text primary key,
                    session_id text not null,
                    turn_index integer not null,
                    recorded_at text not null,
                    unique(session_id, turn_index)
                );
                INSERT INTO agent_turns (turn_id, session_id, turn_index, recorded_at)
                VALUES ('turn_legacy_1', 'ses_legacy_1', 3, '2026-07-11T12:00:00.0000000Z');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        // Pre-v9 turns survive the migration with every pre-existing column value unchanged and
        // null state/usage/diagnosis evidence: migrations never rewrite fact rows.
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM agent_turns;"));
        Assert.Equal("ses_legacy_1", await ScalarStringAsync(connection, "SELECT session_id FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Equal(3L, await ScalarLongAsync(connection, "SELECT turn_index FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Equal("2026-07-11T12:00:00.0000000Z", await ScalarStringAsync(connection, "SELECT recorded_at FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT state FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT prompt_sha256 FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT prompt_tokens FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT diagnostics_kind FROM agent_turns WHERE turn_id = 'turn_legacy_1';"));

        // The column additions are guarded: a second schema pass neither duplicates nor fails.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "agent_turns")).Count(column => column == "state"));
        Assert.Equal(1, (await TableColumnsAsync(connection, "agent_turns")).Count(column => column == "diagnostics"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsSessionProfileColumnsToVersionSevenSessionsWithoutRewritingFactRows()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '7');

                CREATE TABLE agent_sessions(
                    session_id text primary key,
                    attempt_id text,
                    workspace_id text,
                    provider text not null,
                    provider_thread_id text,
                    role text not null,
                    legacy_session_guid text,
                    started_at text not null,
                    completed_at text
                );
                INSERT INTO agent_sessions (
                    session_id, attempt_id, workspace_id, provider, provider_thread_id,
                    role, legacy_session_guid, started_at, completed_at)
                VALUES (
                    'ses_legacy_1', 'att_legacy_1', NULL, 'codex', 'thread-1',
                    'Planning', NULL, '2026-07-11T12:00:00.0000000Z', '2026-07-11T12:01:00.0000000Z');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        // Pre-v8 sessions survive the migration with every pre-existing column value unchanged
        // and null profile evidence: migrations never rewrite fact rows.
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM agent_sessions;"));
        Assert.Equal("att_legacy_1", await ScalarStringAsync(connection, "SELECT attempt_id FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Equal("codex", await ScalarStringAsync(connection, "SELECT provider FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Equal("thread-1", await ScalarStringAsync(connection, "SELECT provider_thread_id FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Equal("Planning", await ScalarStringAsync(connection, "SELECT role FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Equal("2026-07-11T12:00:00.0000000Z", await ScalarStringAsync(connection, "SELECT started_at FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Equal("2026-07-11T12:01:00.0000000Z", await ScalarStringAsync(connection, "SELECT completed_at FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT effort FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT sandbox FROM agent_sessions WHERE session_id = 'ses_legacy_1';"));

        // The column additions are guarded: a second schema pass neither duplicates nor fails.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "agent_sessions")).Count(column => column == "effort"));
        Assert.Equal(1, (await TableColumnsAsync(connection, "agent_sessions")).Count(column => column == "sandbox"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsPolicyColumnToVersionSixAttemptsWithoutRewritingFactRows()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '6');

                CREATE TABLE attempts(
                    attempt_id text primary key,
                    transition_run_id text not null,
                    workflow_instance_id text not null,
                    run_id text not null,
                    attempt_index integer not null,
                    started_at text not null,
                    completed_at text,
                    outcome text,
                    unique(transition_run_id, attempt_index)
                );
                INSERT INTO attempts (
                    attempt_id, transition_run_id, workflow_instance_id, run_id, attempt_index,
                    started_at, completed_at, outcome)
                VALUES (
                    'att_legacy_1', 'tr_legacy_1', 'wfi_legacy_1', 'run_legacy_1', 1,
                    '2026-07-10T12:00:00.0000000Z', '2026-07-10T12:01:00.0000000Z', 'Succeeded');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Contains("policy_id", await TableColumnsAsync(connection, "attempts"));
        // Pre-v7 attempts survive the migration with every pre-existing column value unchanged
        // and a null policy identity: migrations never rewrite fact rows.
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM attempts;"));
        Assert.Equal("tr_legacy_1", await ScalarStringAsync(connection, "SELECT transition_run_id FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal("wfi_legacy_1", await ScalarStringAsync(connection, "SELECT workflow_instance_id FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal("run_legacy_1", await ScalarStringAsync(connection, "SELECT run_id FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT attempt_index FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal("2026-07-10T12:00:00.0000000Z", await ScalarStringAsync(connection, "SELECT started_at FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal("2026-07-10T12:01:00.0000000Z", await ScalarStringAsync(connection, "SELECT completed_at FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Equal("Succeeded", await ScalarStringAsync(connection, "SELECT outcome FROM attempts WHERE attempt_id = 'att_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT policy_id FROM attempts WHERE attempt_id = 'att_legacy_1';"));

        // The column addition is guarded: a second schema pass neither duplicates nor fails.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "attempts")).Count(column => column == "policy_id"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsLineageColumnsToVersionFiveTablesWithoutRewritingFactRows()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '5');

                CREATE TABLE loop_history(
                    kind text not null,
                    sequence integer not null,
                    logical_path text not null unique,
                    body text not null,
                    content_hash text not null,
                    created_at text not null,
                    primary key(kind, sequence)
                );
                INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at)
                VALUES ('Decisions', 1, '.agents/decisions/decisions.0001.md', 'decided', 'hash-1', '2026-07-10T12:00:00.0000000Z');

                CREATE TABLE read_receipts(
                    receipt_id text primary key,
                    run_id text not null,
                    workflow_identity text not null,
                    transition_identity text not null,
                    attempt_id text,
                    commit_hash text,
                    input_surfaces_json text not null,
                    surface_tree_hashes_json text,
                    files_json text not null,
                    products_json text not null,
                    validation text not null,
                    consumed_at text not null
                );
                INSERT INTO read_receipts (
                    receipt_id, run_id, workflow_identity, transition_identity, attempt_id, commit_hash,
                    input_surfaces_json, surface_tree_hashes_json, files_json, products_json, validation, consumed_at)
                VALUES (
                    'rcpt_legacy_1', '', 'Plan', 'WriteExecutablePlan', NULL, NULL,
                    '[]', NULL, '[]', '[]', 'Usable', '2026-07-10T12:00:00.0000000Z');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Contains("history_id", await TableColumnsAsync(connection, "loop_history"));
        Assert.Contains("transition_run_id", await TableColumnsAsync(connection, "read_receipts"));
        // Fact rows survive the migration byte-identical with null lineage: corrections and
        // migrations never rewrite append-only history.
        Assert.Equal("decided", await ScalarStringAsync(connection, "SELECT body FROM loop_history WHERE sequence = 1;"));
        Assert.Equal("hash-1", await ScalarStringAsync(connection, "SELECT content_hash FROM loop_history WHERE sequence = 1;"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT history_id FROM loop_history WHERE sequence = 1;"));
        Assert.Equal("Usable", await ScalarStringAsync(connection, "SELECT validation FROM read_receipts WHERE receipt_id = 'rcpt_legacy_1';"));
        Assert.Null(await ScalarStringAsync(connection, "SELECT transition_run_id FROM read_receipts WHERE receipt_id = 'rcpt_legacy_1';"));

        // The column additions are guarded: a second schema pass neither duplicates nor fails.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "loop_history")).Count(column => column == "history_id"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_UpgradesVersionFourDatabaseToVersionNineIdempotently()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '4');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal("9", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.True(await TableExistsAsync(connection, "read_receipts"), "Expected table `read_receipts` to exist after upgrade.");

        // The migration is idempotent: a second schema pass succeeds and keeps the same shape.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal("9", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.True(await TableExistsAsync(connection, "read_receipts"), "Expected table `read_receipts` to survive a second schema pass.");
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsProductSchemaVersionColumnToPreExistingProductTable()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '4');

                CREATE TABLE canonical_product_records(
                    product_identity text primary key,
                    producer_workflow text not null,
                    producer_transition text not null,
                    intended_consumers_json text not null,
                    repository_ownership text not null,
                    authority text not null,
                    storage_representations_json text not null,
                    causal_identity text not null,
                    freshness text not null,
                    validation_state text not null,
                    lifecycle text not null,
                    evidence_locations_json text not null,
                    updated_at text not null
                );
                INSERT INTO canonical_product_records (
                    product_identity, producer_workflow, producer_transition, intended_consumers_json,
                    repository_ownership, authority, storage_representations_json, causal_identity,
                    freshness, validation_state, lifecycle, evidence_locations_json, updated_at)
                VALUES (
                    'ExecutablePlan', 'Plan', 'WriteExecutablePlan', '[]',
                    'repository-owned', 'canonical', '[]', 'causal',
                    'Fresh', 'Valid', 'Active', '[]', '2026-07-10T12:00:00.0000000Z');
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        IReadOnlyList<string> columns = await TableColumnsAsync(connection, "canonical_product_records");
        Assert.Contains("schema_version", columns);
        Assert.Equal(
            "1",
            await ScalarStringAsync(connection, "SELECT schema_version FROM canonical_product_records WHERE product_identity = 'ExecutablePlan';"));

        // The column addition is guarded: a second schema pass neither duplicates nor fails.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "canonical_product_records")).Count(column => column == "schema_version"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_SeedsWorkspaceIdentityOnceAndKeepsItStableAcrossRuns()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        string firstWorkspaceId;
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            firstWorkspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);
        }

        await using SqliteConnection reopened = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await reopened.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(reopened);
        string secondWorkspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(reopened);

        Assert.Equal(firstWorkspaceId, secondWorkspaceId);
        Assert.Equal(1L, await ScalarLongAsync(reopened, "SELECT COUNT(*) FROM workspace_identity;"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_UpgradesVersionTwoShapedDatabaseToVersionNine()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(legacy, "CREATE TABLE schema_metadata(key text primary key, value text not null);");
            await ExecuteAsync(legacy, "CREATE TABLE workspace_metadata(key text primary key, value text not null);");
            await ExecuteAsync(legacy, "INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '2');");
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal("9", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        foreach (string table in SpineTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected spine table `{table}` to exist after upgrade.");
        }

        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);
        Assert.StartsWith("ws_", workspaceId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureSchemaAsync_MigratesVersionThreeBlockedLabelsAndDropsLegacyLatchTable()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await ExecuteAsync(
                legacy,
                """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '3');

                CREATE TABLE canonical_workflow_states(
                    workflow_identity text primary key,
                    state text not null,
                    current_stage text,
                    outcome text,
                    updated_at text not null,
                    evidence_json text not null
                );
                INSERT INTO canonical_workflow_states (workflow_identity, state, current_stage, outcome, updated_at, evidence_json)
                VALUES ('Plan', 'Blocked', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '[]');

                CREATE TABLE canonical_stage_states(
                    workflow_identity text not null,
                    stage_identity text not null,
                    state text not null,
                    updated_at text not null,
                    evidence_json text not null,
                    primary key(workflow_identity, stage_identity)
                );
                INSERT INTO canonical_stage_states (workflow_identity, stage_identity, state, updated_at, evidence_json)
                VALUES ('Plan', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '[]');

                CREATE TABLE canonical_transition_runs(
                    run_id text primary key,
                    workflow_identity text not null,
                    stage_identity text not null,
                    transition_identity text not null,
                    state text not null,
                    outcome text not null,
                    started_at text not null,
                    completed_at text,
                    input_snapshot_hash text,
                    explanation text not null,
                    evidence_json text not null
                );
                INSERT INTO canonical_transition_runs (
                    run_id, workflow_identity, stage_identity, transition_identity, state, outcome,
                    started_at, completed_at, input_snapshot_hash, explanation, evidence_json)
                VALUES (
                    'run-legacy-1', 'Plan', 'Planning', 'WriteExecutablePlan', 'Blocked', 'Blocked',
                    '2026-07-10T12:00:00.0000000Z', '2026-07-10T12:01:00.0000000Z', NULL, 'legacy blocked run', '[]');

                CREATE TABLE canonical_blockers(
                    blocker_id text primary key,
                    workflow_identity text not null,
                    stage_identity text,
                    transition_identity text,
                    category text not null,
                    reason text not null,
                    authority text not null,
                    required_action text not null,
                    recoverable integer not null,
                    evidence_json text not null,
                    created_at text not null,
                    resolved_at text
                );
                INSERT INTO canonical_blockers (
                    blocker_id, workflow_identity, stage_identity, transition_identity, category,
                    reason, authority, required_action, recoverable, evidence_json, created_at, resolved_at)
                VALUES (
                    'blocker-legacy-1', 'Plan', 'Planning', 'WriteExecutablePlan', 'Validation',
                    'legacy blocker', 'legacy', 'run unblock', 1, '[]', '2026-07-10T12:00:00.0000000Z', NULL);
                """);
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal("9", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal("Resumable", await ScalarStringAsync(connection, "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("MissingRequiredInput", await ScalarStringAsync(connection, "SELECT outcome FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("Resumable", await ScalarStringAsync(connection, "SELECT state FROM canonical_stage_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("InputUnsatisfied", await ScalarStringAsync(connection, "SELECT state FROM canonical_transition_runs WHERE run_id = 'run-legacy-1';"));
        Assert.Equal("MissingRequiredInput", await ScalarStringAsync(connection, "SELECT outcome FROM canonical_transition_runs WHERE run_id = 'run-legacy-1';"));
        Assert.False(await TableExistsAsync(connection, "canonical_blockers"), "Expected legacy `canonical_blockers` table to be dropped.");
        Assert.True(await TableExistsAsync(connection, "evaluation_warnings"), "Expected `evaluation_warnings` table to exist after upgrade.");

        // The label migration is idempotent: a second schema pass leaves the migrated rows unchanged.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal("Resumable", await ScalarStringAsync(connection, "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_ThrowsWhenDatabaseSchemaVersionIsNewerThanSupported()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '10' WHERE key = 'schema_version';");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
    }

    [Fact]
    public async Task ReadWorkspaceIdentityAsync_ThrowsWhenIdentityRowIsMissing()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await ExecuteAsync(connection, "DELETE FROM workspace_identity;");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
    }

    private static async Task RemoveIncomingV9FeaturesAsync(SqliteConnection connection)
    {
        await ExecuteAsync(connection, "DROP TABLE canonical_runtime_prerequisites;");
        foreach (string column in new[]
                 {
                     "state",
                     "prompt_sha256",
                     "prompt_tokens",
                     "output_tokens",
                     "cached_input_tokens",
                     "diagnostics_kind",
                     "diagnostics",
                 })
        {
            await ExecuteAsync(connection, $"ALTER TABLE agent_turns DROP COLUMN {column};");
        }
    }

    private static async Task RemoveMerge4V9FeaturesAsync(SqliteConnection connection)
    {
        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;");
        foreach (string index in new[]
                 {
                     "idx_history_evidence_items_set",
                     "idx_loop_history_history_id",
                     "idx_history_evidence_provider",
                     "idx_history_evidence_recovery",
                     "idx_compatibility_import_events_operation",
                     "idx_projection_effects_status",
                     "idx_transition_recovery_plans_run",
                     "idx_canonical_effect_intents_status",
                     "idx_prompt_dispatch_events_dispatch",
                     "idx_prompt_dispatch_events_attempt",
                     "idx_execution_recommendation_decision",
                     "idx_runtime_profile_evaluation_decision",
                     "idx_decision_scope_lifecycle",
                     "idx_decision_lineage_scope_authority",
                     "idx_decision_lineage_parent",
                     "idx_recovery_attempt_scope_status",
                     "idx_decision_turn_transition",
                 })
        {
            await ExecuteAsync(connection, $"DROP INDEX IF EXISTS {index};");
        }

        foreach (string table in new[]
                 {
                     "workspace_schema_convergences",
                     "workspace_schema_migrations",
                     "workspace_identity_metadata",
                     "session_continuity_profiles",
                     "decision_session_scopes",
                     "decision_session_lineage",
                     "decision_session_active",
                     "session_recovery_plans",
                     "session_recovery_attempts",
                     "session_recovery_sources",
                     "decision_session_turns",
                     "session_transition_correlations",
                     "decision_session_legacy_imports",
                     "history_evidence_sets",
                     "history_evidence_items",
                     "compatibility_import_operations",
                     "compatibility_import_events",
                     "canonical_projection_effects",
                     "transition_recovery_plans",
                     "canonical_effect_intents",
                     "execution_recommendation_evidence",
                     "runtime_profile_evaluations",
                     "prompt_dispatch_events",
                     "persistence_projection_checkpoints",
                 })
        {
            await ExecuteAsync(connection, $"DROP TABLE IF EXISTS {table};");
        }

        foreach ((string table, string column) in new (string Table, string Column)[]
                 {
                     ("loop_history", "workspace_id"),
                     ("loop_history", "workflow_instance_id"),
                     ("loop_history", "session_id"),
                     ("loop_history", "turn_id"),
                     ("loop_history", "supersedes_id"),
                     ("loop_history", "producer_run_id"),
                     ("loop_history", "producer_lineage_id"),
                     ("loop_history", "provider_thread_id"),
                     ("loop_history", "provider_turn_id"),
                     ("loop_history", "recovery_attempt_id"),
                     ("canonical_rendered_prompts", "persistence_id"),
                     ("canonical_rendered_prompts", "prompt_policy_profile_id"),
                     ("canonical_rendered_prompts", "consumed_input_manifest_id"),
                     ("canonical_rendered_prompts", "rendered_encoding"),
                     ("session_telemetry_events", "provider_thread_id"),
                     ("session_telemetry_events", "lineage_id"),
                     ("session_telemetry_events", "transition_run_id"),
                     ("session_telemetry_events", "recovery_attempt_id"),
                     ("session_telemetry_events", "continuity_event_type"),
                     ("session_telemetry_events", "continuity_outcome"),
                 })
        {
            await ExecuteAsync(connection, $"ALTER TABLE {table} DROP COLUMN {column};");
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;");
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-schema-v7-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static string CreateDatabasePath(Repository repository)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return databasePath;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt64(scalar) == 1;
    }

    private static async Task<IReadOnlyList<string>> TableColumnsAsync(SqliteConnection connection, string table)
    {
        List<string> columns = [];
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        return columns;
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt64(scalar);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}

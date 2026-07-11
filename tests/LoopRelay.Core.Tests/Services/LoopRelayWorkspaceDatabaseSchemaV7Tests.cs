using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

public sealed class LoopRelayWorkspaceDatabaseSchemaV7Tests
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
    public async Task EnsureSchemaAsync_FreshDatabase_StampsVersionSevenAndCreatesSpineTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(7, LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
        Assert.Equal("7", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
    public async Task EnsureSchemaAsync_UpgradesVersionFourDatabaseToVersionSevenIdempotently()
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

        Assert.Equal("7", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.True(await TableExistsAsync(connection, "read_receipts"), "Expected table `read_receipts` to exist after upgrade.");

        // The migration is idempotent: a second schema pass succeeds and keeps the same shape.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal("7", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
    public async Task EnsureSchemaAsync_UpgradesVersionTwoShapedDatabaseToVersionSeven()
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

        Assert.Equal("7", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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

        Assert.Equal("7", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '8' WHERE key = 'schema_version';");

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

using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

public sealed class LoopRelayWorkspaceDatabaseSchemaV4Tests
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
    public async Task EnsureSchemaAsync_FreshDatabase_StampsVersionFourAndCreatesSpineTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(4, LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
        Assert.Equal("4", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
    public async Task EnsureSchemaAsync_UpgradesVersionTwoShapedDatabaseToVersionFour()
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

        Assert.Equal("4", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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

        Assert.Equal("4", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '5' WHERE key = 'schema_version';");

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
        string path = Directory.CreateTempSubdirectory("looprelay-schema-v4-").FullName;
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

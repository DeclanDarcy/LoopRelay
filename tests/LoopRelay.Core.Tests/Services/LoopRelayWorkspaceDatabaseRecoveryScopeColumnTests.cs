using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers the v15 -> v16 convergence that denormalises <c>scopeId</c> out of
/// <c>canonical_recovery_action_events.document_json</c> into a real, indexed
/// <c>scope_id</c> column (PERF-20).
///
/// <para>
/// The interesting case is not the fresh-create path - that one is proved by the shape
/// verification the migration already runs. It is an <b>existing</b> database stamped at the
/// pre-change v15 shape, whose action-event rows only ever carried the scope inside JSON.
/// These tests reconstruct exactly that file and open it.
/// </para>
/// </summary>
[Collection("WorkspaceDatabaseCounters")]
public sealed class LoopRelayWorkspaceDatabaseRecoveryScopeColumnTests
{
    [Fact]
    public async Task EnsureSchemaAsync_backfills_recovery_scope_id_on_an_existing_pre_v16_database()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);

        await RevertToCanonicalV15ShapeAsync(connection);
        await SeedPreV16ActionEventsAsync(connection);

        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        WorkspaceSchemaInspection before = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV15Complete, before.Shape);
        Assert.Equal(LoopRelayWorkspaceDatabase.CanonicalV15ShapeFingerprint, before.ShapeFingerprint);

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        WorkspaceSchemaInspection after = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV16Complete, after.Shape);
        Assert.Equal("16", await ScalarStringAsync(
            connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal(workspaceId, await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection));
        Assert.Equal(1L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM workspace_schema_migrations WHERE from_version = 15 AND to_version = 16;"));

        // The row whose scope only ever existed inside document_json is now denormalised.
        Assert.Equal("scope-alpha", await ScalarStringAsync(
            connection, "SELECT scope_id FROM canonical_recovery_action_events WHERE action_id = 'attempt-alpha';"));
        Assert.Equal("scope-beta", await ScalarStringAsync(
            connection, "SELECT scope_id FROM canonical_recovery_action_events WHERE action_id = 'attempt-beta';"));

        // Rows that carry no scope stay NULL rather than failing the migration: `document_json` is
        // nullable (the plain action-journal writer never populates it) and a document without a
        // `scopeId` member extracts to NULL.
        Assert.Null(await ScalarStringAsync(
            connection, "SELECT scope_id FROM canonical_recovery_action_events WHERE action_id = 'action-no-document';"));
        Assert.Null(await ScalarStringAsync(
            connection, "SELECT scope_id FROM canonical_recovery_action_events WHERE action_id = 'action-no-scope-member';"));

        Assert.True(
            await IndexExistsAsync(connection, "idx_recovery_action_events_scope"),
            "Expected the migration to create `idx_recovery_action_events_scope`.");
        Assert.Equal(
            "text",
            await ColumnTypeAsync(connection, "canonical_recovery_action_events", "scope_id"),
            StringComparer.OrdinalIgnoreCase);

        // Re-running the convergence is idempotent: the column is added once, never duplicated.
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal(1, (await TableColumnsAsync(connection, "canonical_recovery_action_events"))
            .Count(column => string.Equals(column, "scope_id", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Physically returns a freshly converged database to the pre-change v15 shape: drops the new
    /// index and column, then re-stamps the v15 version and shape fingerprint so the classifier
    /// sees a legitimately complete canonical-v15 file rather than a corrupt one.
    /// </summary>
    private static async Task RevertToCanonicalV15ShapeAsync(SqliteConnection connection)
    {
        await ExecuteAsync(connection, "DROP INDEX IF EXISTS idx_recovery_action_events_scope;");
        await ExecuteAsync(connection, "ALTER TABLE canonical_recovery_action_events DROP COLUMN scope_id;");
        await ExecuteAsync(
            connection,
            "UPDATE schema_metadata SET value = '15' WHERE key = 'schema_version';");
        await ExecuteAsync(
            connection,
            "UPDATE schema_metadata SET value = $shape WHERE key = 'schema_shape';",
            ("$shape", LoopRelayWorkspaceDatabase.CanonicalV15ShapeFingerprint));
    }

    private static async Task SeedPreV16ActionEventsAsync(SqliteConnection connection) =>
        await ExecuteAsync(
            connection,
            """
            INSERT INTO canonical_recovery_action_events (
                action_id, plan_id, lifecycle, explanation, evidence_json, document_json, recorded_at
            ) VALUES
                ('attempt-alpha', 'plan-alpha', 'Planned', 'alpha', '[]',
                 '{"attemptId":"attempt-alpha","scopeId":"scope-alpha"}', '2026-07-12T00:00:00.0000000Z'),
                ('attempt-beta', 'plan-beta', 'Planned', 'beta', '[]',
                 '{"attemptId":"attempt-beta","scopeId":"scope-beta"}', '2026-07-12T00:00:01.0000000Z'),
                ('action-no-document', 'plan-alpha', 'Started', 'no document', '[]',
                 NULL, '2026-07-12T00:00:02.0000000Z'),
                ('action-no-scope-member', 'plan-alpha', 'Started', 'no scope member', '[]',
                 '{"attemptId":"attempt-alpha"}', '2026-07-12T00:00:03.0000000Z');
            """);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-recovery-scope-").FullName;
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

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> IndexExistsAsync(SqliteConnection connection, string index)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $index;";
        command.Parameters.AddWithValue("$index", index);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<string?> ColumnTypeAsync(SqliteConnection connection, string table, string column)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT type FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
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
}

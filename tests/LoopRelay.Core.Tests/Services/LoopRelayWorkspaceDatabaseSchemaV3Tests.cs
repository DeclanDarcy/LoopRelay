using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

public sealed class LoopRelayWorkspaceDatabaseSchemaV3Tests
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
    public async Task EnsureSchemaAsync_FreshDatabase_StampsVersionThreeAndCreatesSpineTables()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal(3, LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
        Assert.Equal("3", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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
    public async Task EnsureSchemaAsync_UpgradesVersionTwoShapedDatabaseToVersionThree()
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

        Assert.Equal("3", await ScalarStringAsync(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        foreach (string table in SpineTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"Expected spine table `{table}` to exist after upgrade.");
        }

        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection);
        Assert.StartsWith("ws_", workspaceId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureSchemaAsync_ThrowsWhenDatabaseSchemaVersionIsNewerThanSupported()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = '4' WHERE key = 'schema_version';");

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
        string path = Directory.CreateTempSubdirectory("looprelay-schema-v3-").FullName;
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

using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

public sealed record WorkspaceSchemaVersionManifest(
    int Version,
    string SchemaIdentity,
    string SchemaFamily,
    string ShapeFingerprint);

public static class WorkspaceSchemaMigrationCatalog
{
    public static WorkspaceSchemaVersionManifest Current { get; } = new(
        LoopRelayWorkspaceDatabase.CurrentSchemaVersion,
        LoopRelayWorkspaceDatabase.SchemaIdentity,
        LoopRelayWorkspaceDatabase.SchemaFamily,
        LoopRelayWorkspaceDatabase.CanonicalV15ShapeFingerprint);

    public static IReadOnlyList<int> Plan(int? sourceVersion)
    {
        int source = sourceVersion ?? 0;
        if (source > Current.Version) return [];
        return Enumerable.Range(Math.Max(1, source + 1), Current.Version - source).ToArray();
    }
}

public static class WorkspaceDatabaseConnectionFactory
{
    public static SqliteConnection OpenReadOnly(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
    }

    public static SqliteConnection OpenMigrationTarget(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadWriteCreate(Path.GetFullPath(databasePath));
}

public sealed class WorkspaceSchemaMigrationExecutor
{
    public async Task<WorkspaceSchemaInspection> ExecuteAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection, cancellationToken);
    }
}

public sealed class WorkspaceSchemaReadOnlyInspector
{
    public async Task<WorkspaceSchemaInspection> InspectAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath))
            return new WorkspaceSchemaInspection(null, WorkspaceSchemaFamily.Empty, null, false,
                WorkspaceSchemaShape.Empty, null, "Workspace database does not exist.");
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        return await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection, cancellationToken);
    }

    public async Task<string?> ReadWorkspaceIdentityAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT workspace_id FROM workspace_identity WHERE id = 1;";
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}

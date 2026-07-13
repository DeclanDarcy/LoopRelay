using System.Globalization;
using System.Security.Cryptography;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Storage;

public sealed class WorkspaceStorageInspector : IWorkspaceStorageInspector
{
    public async Task<StorageInspection> VerifyAsync(
        StorageVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(request.RepositoryPath);
        string database = Path.Combine(root,
            LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        string persistence = Path.GetDirectoryName(database)!;
        IReadOnlyList<StorageTreeEntry> inventory = await InventoryAsync(root, persistence, cancellationToken);
        if (!File.Exists(database))
        {
            return new StorageInspection(
                StorageHealth.ActionRequired, false, null, null, null, inventory, [],
                Interrupted(inventory), ["Run `storage init` to create a new canonical authority."],
                inventory.Select(item => item.RelativePath).ToArray());
        }

        long length = new FileInfo(database).Length;
        string byteHash = await HashFileAsync(database, cancellationToken);
        WorkspaceSchemaInspection schema;
        IReadOnlyList<string> unresolved;
        try
        {
            schema = await new WorkspaceSchemaReadOnlyInspector().InspectAsync(database, cancellationToken);
            unresolved = await ForeignKeyViolationsAsync(database, cancellationToken);
        }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException)
        {
            return new StorageInspection(
                StorageHealth.Corrupt, true, length, byteHash, null, inventory, [], Interrupted(inventory),
                ["Restore or explicitly repair the corrupt workspace authority."],
                [exception.GetType().Name, "SQLite authority is unreadable."]);
        }

        string[] interrupted = Interrupted(inventory)
            .Concat(await InterruptedRowsAsync(database, cancellationToken))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        StorageHealth health;
        var actions = new List<string>();
        if (schema.Version is > LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
        {
            health = StorageHealth.Unsupported;
            actions.Add($"Use a LoopRelay version supporting schema v{schema.Version}.");
        }
        else if (schema.Shape == WorkspaceSchemaShape.CanonicalV15Complete &&
                 unresolved.Count == 0 && interrupted.Length == 0)
        {
            health = StorageHealth.Healthy;
        }
        else if (schema.Family == WorkspaceSchemaFamily.CanonicalWorkspace &&
                 schema.Shape is not (WorkspaceSchemaShape.Unknown or WorkspaceSchemaShape.UnknownV9Shape or
                     WorkspaceSchemaShape.CorruptCanonicalV9 or WorkspaceSchemaShape.CorruptCanonicalV10 or
                     WorkspaceSchemaShape.CorruptCanonicalV11 or WorkspaceSchemaShape.CorruptCanonicalV12 or
                     WorkspaceSchemaShape.CorruptCanonicalV13 or WorkspaceSchemaShape.CorruptCanonicalV14 or
                     WorkspaceSchemaShape.CorruptCanonicalV15))
        {
            health = StorageHealth.ActionRequired;
            string chain = string.Join(" -> ", WorkspaceSchemaMigrationCatalog.Plan(schema.Version));
            actions.Add($"Run `storage migrate`; planned target versions: {chain}.");
        }
        else
        {
            health = StorageHealth.Corrupt;
            actions.Add("Use explicit compatibility import or repair; verification will not mutate this authority.");
        }
        if (unresolved.Count > 0) actions.Add("Resolve canonical foreign-key references before mutation.");
        if (interrupted.Length > 0) actions.Add("Recover the interrupted storage operation before starting another.");

        return new StorageInspection(
            health, true, length, byteHash, schema, inventory, unresolved, interrupted, actions,
            [$"schema:{schema.SchemaIdentity ?? "unknown"}", $"family:{schema.Family}",
             $"version:{schema.Version?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}",
             $"shape:{schema.Shape}", $"shape-fingerprint:{schema.ShapeFingerprint ?? "unknown"}",
             $"bytes-sha256:{byteHash}"]);
    }

    private static async Task<IReadOnlyList<StorageTreeEntry>> InventoryAsync(
        string root,
        string persistence,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(persistence)) return [];
        var result = new List<StorageTreeEntry>();
        foreach (string file in Directory.GetFiles(persistence, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            result.Add(new StorageTreeEntry(
                Path.GetRelativePath(root, file).Replace('\\', '/'),
                new FileInfo(file).Length,
                await HashFileAsync(file, cancellationToken)));
        }
        return result;
    }

    private static string[] Interrupted(IReadOnlyList<StorageTreeEntry> inventory) => inventory
        .Where(item => item.RelativePath.EndsWith("-journal", StringComparison.OrdinalIgnoreCase) ||
                       item.RelativePath.EndsWith(".storage-stage", StringComparison.OrdinalIgnoreCase))
        .Select(item => item.RelativePath)
        .ToArray();

    private static async Task<IReadOnlyList<string>> InterruptedRowsAsync(
        string database,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        var result = new List<string>();
        if (await TableExistsAsync(connection, "workflow_transactions", cancellationToken))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT transaction_id, status FROM workflow_transactions
                WHERE status <> 'Completed' OR completed_at IS NULL ORDER BY started_at, transaction_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add($"workflow_transactions:{reader.GetString(0)}:{reader.GetString(1)}");
        }
        if (await TableExistsAsync(connection, "canonical_storage_operation_plans", cancellationToken))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT operation_id, current_lifecycle FROM canonical_storage_operation_plans
                WHERE current_lifecycle NOT IN ('Completed','Refused') ORDER BY planned_at, operation_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add($"storage-operation:{reader.GetString(0)}:{reader.GetString(1)}");
        }
        return result;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$table;";
        command.Parameters.AddWithValue("$table", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<IReadOnlyList<string>> ForeignKeyViolationsAsync(
        string database,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        var result = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add($"{reader.GetString(0)}:{reader.GetInt64(1)}:{(reader.IsDBNull(2) ? "unknown" : reader.GetString(2))}");
        return result;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}

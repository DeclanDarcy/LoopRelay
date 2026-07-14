using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public static class CanonicalCheckpointKeys
{
    public const string PlanWarmSession = "plan_warm_session.v1";
    public const string ExecutionWarmSession = "execution_warm_session.v1";
    public const string CompletionCertification = "completion_certification.v1";
}

public sealed class CanonicalCheckpointStore(Repository _repository)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO workspace_metadata(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(value, Options));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database)) return default;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM workspace_metadata WHERE key=$key;";
        command.Parameters.AddWithValue("$key", key);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public async Task RetireAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database)) return;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_metadata WHERE key=$key;";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

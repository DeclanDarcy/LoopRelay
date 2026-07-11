using System.Text.Json;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

internal sealed record CompletionCertificationCheckpoint(
    CompletionCertificationResult Result,
    IReadOnlyList<string> RecoveryEvidencePaths,
    DateTimeOffset RecordedAt);

internal sealed class CompletionCertificationCheckpointStore(Repository _repository)
{
    private const string MetadataKey = "completion_certification.v1";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        CompletionCertificationCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workspace_metadata (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", MetadataKey);
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(checkpoint, Options));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CompletionCertificationCheckpoint?> ReadAsync(CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM workspace_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", MetadataKey);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json) return null;
        try
        {
            return JsonSerializer.Deserialize<CompletionCertificationCheckpoint>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task RetireAsync(CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(database)) return;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", MetadataKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

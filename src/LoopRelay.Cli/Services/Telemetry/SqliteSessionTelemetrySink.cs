using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Canonical SQLite sink for per-turn session telemetry events.
/// </summary>
internal sealed class SqliteSessionTelemetrySink(Repository repository) : ISessionTelemetrySink
{
    private readonly object gate = new();

    private string RuntimeDirectoryPath => Path.Combine(repository.Path, ".LoopRelay");

    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(repository);

    public void Append(SessionTelemetryRecord record)
    {
        string json = JsonSerializer.Serialize(record, SessionTelemetryJson.Options);
        lock (gate)
        {
            Directory.CreateDirectory(RuntimeDirectoryPath);
            EnsureSelfIgnore();
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

            using var connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(DatabasePath);
            connection.Open();
            LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection).GetAwaiter().GetResult();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO session_telemetry_events (
                    recorded_at,
                    repo_name,
                    session_id,
                    session_type,
                    turn_index,
                    document_json,
                    content_hash)
                VALUES (
                    $recorded_at,
                    $repo_name,
                    $session_id,
                    $session_type,
                    $turn_index,
                    $document_json,
                    $content_hash);
                """;
            command.Parameters.AddWithValue("$recorded_at", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$repo_name", record.RepoName);
            command.Parameters.AddWithValue("$session_id", record.SessionId);
            command.Parameters.AddWithValue("$session_type", record.SessionType);
            command.Parameters.AddWithValue("$turn_index", record.TurnIndex);
            command.Parameters.AddWithValue("$document_json", json);
            command.Parameters.AddWithValue("$content_hash", Sha256(json));
            command.ExecuteNonQuery();
        }
    }

    private void EnsureSelfIgnore()
    {
        string gitignore = Path.Combine(RuntimeDirectoryPath, ".gitignore");
        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, "*\n");
        }
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}

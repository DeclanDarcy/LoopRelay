using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Cli;

internal sealed record LedgerEvidenceMatch(
    string Source,
    string Identity,
    string Content);

/// <summary>
/// Resolves receipt-consumed content back out of the evidence ledger by its sha256: system-owned
/// bodies (for example the adversarial review under gitignored <c>.LoopRelay/evidence</c>) are
/// captured as raw prompt output in <c>canonical_transition_evidence</c>, and rotated
/// decision/handoff/delta bodies live in <c>loop_history</c>. A read receipt's file hash is
/// therefore retrievable exactly as consumed without the file surviving on disk.
/// </summary>
internal static class LedgerEvidenceRetrieval
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<LedgerEvidenceMatch?> TryResolveContentByHashAsync(
        Repository repository,
        string sha256,
        CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return null;
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        return await TryResolveFromLoopHistoryAsync(connection, sha256, cancellationToken)
            ?? await TryResolveFromRawPromptOutputAsync(connection, sha256, cancellationToken);
    }

    private static async Task<LedgerEvidenceMatch?> TryResolveFromLoopHistoryAsync(
        SqliteConnection connection,
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            // Pre-v6 databases lack history_id but their bodies are still retrievable by hash;
            // the logical path stands in as the row's identity there.
            bool hasHistoryId = await ColumnExistsAsync(connection, "loop_history", "history_id", cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasHistoryId
                ? """
                  SELECT body, history_id, logical_path
                  FROM loop_history
                  WHERE content_hash = $content_hash
                  ORDER BY kind, sequence DESC
                  LIMIT 1;
                  """
                : """
                  SELECT body, NULL, logical_path
                  FROM loop_history
                  WHERE content_hash = $content_hash
                  ORDER BY kind, sequence DESC
                  LIMIT 1;
                  """;
            command.Parameters.AddWithValue("$content_hash", sha256);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            string body = reader.GetString(0);
            string identity = reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1);
            return new LedgerEvidenceMatch("loop_history", identity, body);
        }
        catch (SqliteException)
        {
            // Databases that predate the loop_history table; the raw-prompt-output source below
            // is still consulted.
            return null;
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<LedgerEvidenceMatch?> TryResolveFromRawPromptOutputAsync(
        SqliteConnection connection,
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT evidence_id, document_json
                FROM canonical_transition_evidence
                WHERE event_name = 'RawPromptOutputCaptured'
                ORDER BY evidence_id DESC;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                PromptExecutionResult? execution;
                try
                {
                    execution = JsonSerializer.Deserialize<PromptExecutionResult>(reader.GetString(1), JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (execution is not null &&
                    string.Equals(ConsumedInputFile.HashContent(execution.RawOutput), sha256, StringComparison.Ordinal))
                {
                    return new LedgerEvidenceMatch(
                        "canonical_transition_evidence",
                        reader.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        execution.RawOutput);
                }
            }

            return null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}

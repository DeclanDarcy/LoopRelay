using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public sealed record CanonicalLedgerEvidenceMatch(string Source, string Identity, string Content);

public interface ICanonicalLedgerEvidenceProjection
{
    Task<CanonicalLedgerEvidenceMatch?> TryResolveContentByHashAsync(
        Repository repository,
        string sha256,
        CancellationToken cancellationToken = default);
}

public sealed class CanonicalLedgerEvidenceProjection : ICanonicalLedgerEvidenceProjection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CanonicalLedgerEvidenceMatch?> TryResolveContentByHashAsync(
        Repository repository,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        if (!File.Exists(databasePath)) return null;
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        return await TryResolveFromLoopHistoryAsync(connection, sha256, cancellationToken)
            ?? await TryResolveFromRawPromptOutputAsync(connection, sha256, cancellationToken);
    }

    private static async Task<CanonicalLedgerEvidenceMatch?> TryResolveFromLoopHistoryAsync(
        SqliteConnection connection,
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            bool hasHistoryId = await ColumnExistsAsync(connection, "loop_history", "history_id", cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = hasHistoryId
                ? "SELECT body,history_id,logical_path FROM loop_history WHERE content_hash=$hash ORDER BY kind,sequence DESC LIMIT 1;"
                : "SELECT body,NULL,logical_path FROM loop_history WHERE content_hash=$hash ORDER BY kind,sequence DESC LIMIT 1;";
            command.Parameters.AddWithValue("$hash", sha256);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new CanonicalLedgerEvidenceMatch(
                "loop_history", reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1), reader.GetString(0));
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    private static async Task<CanonicalLedgerEvidenceMatch?> TryResolveFromRawPromptOutputAsync(
        SqliteConnection connection,
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT evidence_id,document_json FROM canonical_transition_evidence WHERE event_name='RawPromptOutputCaptured' ORDER BY evidence_id DESC;";
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
                if (execution is not null && string.Equals(
                    ConsumedInputFile.HashContent(execution.RawOutput), sha256, StringComparison.Ordinal))
                    return new CanonicalLedgerEvidenceMatch(
                        "canonical_transition_evidence",
                        reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                        execution.RawOutput);
            }
            return null;
        }
        catch (SqliteException)
        {
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
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name=$column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }
}

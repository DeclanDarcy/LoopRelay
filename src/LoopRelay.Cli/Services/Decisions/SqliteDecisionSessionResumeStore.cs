using System.Globalization;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Decisions;

/// <summary>
/// SQLite-canonical decision-session resume store. Legacy file import is exhausted and absent.
/// </summary>
internal sealed class SqliteDecisionSessionResumeStore(
    Repository repository,
    Action<string>? onWarning = null) : IDecisionSessionResumeStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(repository);

    public async Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadSqliteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not read decision-session resume state in {DatabasePath}: {ex.Message}");
            return null;
        }
    }

    public async Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteSqliteAsync(state, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not persist decision-session resume state in {DatabasePath}: {ex.Message}");
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(DatabasePath))
            {
                await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(DatabasePath);
                await connection.OpenAsync(cancellationToken);
                await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM decision_session_resume WHERE id = 1;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not delete decision-session resume state in {DatabasePath}: {ex.Message}");
        }
    }

    public void EnsureDirectoryProtection()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(DatabasePath);
            connection.Open();
            LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            onWarning?.Invoke($"Could not prepare runtime SQLite persistence at {DatabasePath}: {ex.Message}");
        }
    }

    private async Task<DecisionSessionResumeState?> ReadSqliteAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(DatabasePath))
        {
            return null;
        }

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(DatabasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json FROM decision_session_resume WHERE id = 1;";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is null or DBNull)
        {
            return null;
        }

        DecisionSessionResumeState? state = JsonSerializer.Deserialize<DecisionSessionResumeState>(
            Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty,
            Json);
        if (IsUsable(state))
        {
            return state;
        }

        onWarning?.Invoke(
            $"Ignoring unusable decision-session resume state in {DatabasePath} (schema/content mismatch) - clearing it.");
        await ClearSqliteAsync(connection, cancellationToken);
        return null;
    }

    private async Task WriteSqliteAsync(
        DecisionSessionResumeState state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        DecisionSessionResumeState stamped = state with { SavedAtUtc = DateTimeOffset.UtcNow };
        string json = JsonSerializer.Serialize(stamped, Json);
        string savedAt = stamped.SavedAtUtc.ToString("O", CultureInfo.InvariantCulture);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(DatabasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO decision_session_resume (id, document_json, saved_at)
            VALUES (1, $document_json, $saved_at)
            ON CONFLICT(id) DO UPDATE SET
                document_json = excluded.document_json,
                saved_at = excluded.saved_at;
            """;
        command.Parameters.AddWithValue("$document_json", json);
        command.Parameters.AddWithValue("$saved_at", savedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearSqliteAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM decision_session_resume WHERE id = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsUsable(DecisionSessionResumeState? state) =>
        state is not null &&
        state.SchemaVersion == DecisionSessionResumeState.CurrentSchemaVersion &&
        !string.IsNullOrWhiteSpace(state.ThreadId);

}

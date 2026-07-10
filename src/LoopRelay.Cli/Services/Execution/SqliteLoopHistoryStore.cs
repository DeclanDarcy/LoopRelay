using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

internal static class LoopHistoryStoreFactory
{
    public static ILoopHistoryStore Create(IArtifactStore store, Repository repository) =>
        LoopWorkspaceDatabase.HasUsableLoopHistoryDatabase(repository)
            ? new SqliteLoopHistoryStore(repository)
            : new FileBackedLoopHistoryStore(store, repository);
}

internal static class LoopWorkspaceDatabase
{
    public const int CurrentSchemaVersion = LoopRelayWorkspaceDatabase.CurrentSchemaVersion;
    public const string RelativeDatabasePath = LoopRelayWorkspaceDatabase.RelativeDatabasePath;

    public static string Resolve(Repository repository) =>
        LoopRelayWorkspaceDatabase.Resolve(repository);

    public static bool HasUsableLoopHistoryDatabase(Repository repository)
    {
        string databasePath = Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
            connection.Open();
            LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection).GetAwaiter().GetResult();
            string? version = ScalarString(
                connection,
                "SELECT value FROM schema_metadata WHERE key = 'schema_version';");
            if (!string.Equals(
                version,
                CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
            {
                return false;
            }

            string? state = ScalarString(
                connection,
                "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';");
            if (state is not "imported" and not "canonical")
            {
                return false;
            }

            long tables = ScalarLong(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'loop_history';");
            return tables == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static SqliteConnection OpenReadOnly(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);

    internal static SqliteConnection OpenReadWrite(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);

    private static string? ScalarString(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = command.ExecuteScalar();
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static long ScalarLong(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = command.ExecuteScalar();
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }
}

internal sealed class SqliteLoopHistoryStore(Repository repository) : ILoopHistoryStore
{
    public async Task<LoopHistoryRecord> AppendAsync(
        LoopHistoryKind kind,
        string content,
        LoopHistoryProducerCorrelation? producer = null)
    {
        LoopHistorySpec spec = GetSpec(kind);
        string databasePath = LoopWorkspaceDatabase.Resolve(repository);
        await using SqliteConnection connection = LoopWorkspaceDatabase.OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        int sequence = await NextSequenceAsync(connection, transaction, spec);
        string relativePath = spec.HistoricalPath(sequence);

        string insert = producer is null
            ? """
            INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at)
            VALUES ($kind, $sequence, $logical_path, $body, $content_hash, $created_at);
            """
            : """
            INSERT INTO loop_history (
                kind, sequence, logical_path, body, content_hash, created_at,
                producer_run_id, producer_lineage_id, provider_thread_id, provider_turn_id, recovery_attempt_id)
            VALUES (
                $kind, $sequence, $logical_path, $body, $content_hash, $created_at,
                $producer_run_id, $producer_lineage_id, $provider_thread_id, $provider_turn_id, $recovery_attempt_id);
            """;
        await ExecuteAsync(
            connection,
            transaction,
            insert,
            ("$kind", spec.KindToken),
            ("$sequence", sequence),
            ("$logical_path", relativePath),
            ("$body", content),
            ("$content_hash", Sha256(content)),
            ("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
            ("$producer_run_id", producer?.TransitionRunId),
            ("$producer_lineage_id", producer?.LineageId),
            ("$provider_thread_id", producer?.ProviderThreadId),
            ("$provider_turn_id", producer?.ProviderTurnId),
            ("$recovery_attempt_id", producer?.RecoveryAttemptId));
        await transaction.CommitAsync();

        return new LoopHistoryRecord(kind, sequence, relativePath, content, producer);
    }

    public async Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind)
    {
        LoopHistorySpec spec = GetSpec(kind);
        string databasePath = LoopWorkspaceDatabase.Resolve(repository);
        await using SqliteConnection connection = LoopWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        bool hasProducerColumns = await HasColumnAsync(connection, "loop_history", "producer_run_id");
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = hasProducerColumns ? """
            SELECT sequence, logical_path, body, content_hash,
                   producer_run_id, producer_lineage_id, provider_thread_id, provider_turn_id, recovery_attempt_id
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence DESC
            LIMIT 1;
            """ : """
            SELECT sequence, logical_path, body, content_hash
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        int sequence = reader.GetInt32(0);
        string relativePath = reader.GetString(1);
        string body = reader.GetString(2);
        string hash = reader.GetString(3);
        if (!string.Equals(hash, Sha256(body), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Loop history hash mismatch for `{relativePath}`.");
        }

        LoopHistoryProducerCorrelation? producer = !hasProducerColumns
            || reader.IsDBNull(4) || reader.IsDBNull(5) || reader.IsDBNull(6)
            ? null
            : new LoopHistoryProducerCorrelation(
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8));
        return new LoopHistoryRecord(kind, sequence, relativePath, body, producer);
    }

    private static async Task<bool> HasColumnAsync(SqliteConnection connection, string table, string column)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<int> NextSequenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LoopHistorySpec spec)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(sequence), 0) FROM loop_history WHERE kind = $kind;";
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture) + 1;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static LoopHistorySpec GetSpec(LoopHistoryKind kind) => kind switch
    {
        LoopHistoryKind.Decisions => new LoopHistorySpec(
            "Decisions",
            OrchestrationArtifactPaths.HistoricalDecision),
        LoopHistoryKind.Handoff => new LoopHistorySpec(
            "Handoff",
            OrchestrationArtifactPaths.HistoricalHandoff),
        LoopHistoryKind.OperationalDelta => new LoopHistorySpec(
            "OperationalDelta",
            OrchestrationArtifactPaths.HistoricalDelta),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private readonly record struct LoopHistorySpec(
        string KindToken,
        Func<int, string> HistoricalPath);
}

using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite.Abstractions;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CommandCenter.Persistence.Sqlite;

/// <summary>
/// Dapper-backed <see cref="IRecoveryResultStore"/> over the per-repo <c>recovery_result</c> table
/// (refactor-lazy-sqlite.md, Phase 4). Each row stores its envelope as a <c>payload_json</c> text column
/// round-tripped through the caller-supplied <see cref="JsonSerializerOptions"/> (the decision-session DI wires
/// <c>DecisionSessionJson.Options</c>), so the <c>DecisionSessionRecoveryResult</c> wire shape is byte-stable
/// versus the prior file path. A write is an atomic UPSERT on the <c>(repository_id, recovery_id)</c> primary
/// key; a list returns rows ordered <c>occurred_at</c> asc then <c>recovery_id</c> ordinal to match the file
/// path's <c>GetHistoryAsync</c> ordering.
/// </summary>
public sealed class SqliteRecoveryResultStore : IRecoveryResultStore
{
    private readonly ISqliteConnectionFactory connectionFactory;
    private readonly JsonSerializerOptions payloadOptions;

    public SqliteRecoveryResultStore(
        ISqliteConnectionFactory connectionFactory,
        JsonSerializerOptions payloadOptions)
    {
        this.connectionFactory = connectionFactory;
        this.payloadOptions = payloadOptions;
    }

    public async Task<IReadOnlyList<TResult>> ListAsync<TResult>(Repository repo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        await using SqliteConnection connection =
            await connectionFactory.OpenRepositoryConnectionAsync(repo, ct).ConfigureAwait(false);

        var command = new CommandDefinition(
            """
            SELECT payload_json
            FROM recovery_result
            WHERE repository_id = @RepositoryId
            ORDER BY occurred_at ASC, recovery_id ASC;
            """,
            new { RepositoryId = repo.Id.ToString() },
            cancellationToken: ct);

        IEnumerable<string> payloads = await connection.QueryAsync<string>(command).ConfigureAwait(false);

        var results = new List<TResult>();
        foreach (string payloadJson in payloads)
        {
            TResult? result = JsonSerializer.Deserialize<TResult>(payloadJson, payloadOptions);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public async Task WriteAsync<TResult>(
        Repository repo, string recoveryId, DateTimeOffset occurredAt, TResult result, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        await using SqliteConnection connection =
            await connectionFactory.OpenRepositoryConnectionAsync(repo, ct).ConfigureAwait(false);

        string payloadJson = JsonSerializer.Serialize(result, payloadOptions);

        var command = new CommandDefinition(
            """
            INSERT INTO recovery_result
                (repository_id, recovery_id, occurred_at, payload_json)
            VALUES
                (@RepositoryId, @RecoveryId, @OccurredAt, @PayloadJson)
            ON CONFLICT (repository_id, recovery_id) DO UPDATE SET
                occurred_at  = excluded.occurred_at,
                payload_json = excluded.payload_json;
            """,
            new
            {
                RepositoryId = repo.Id.ToString(),
                RecoveryId = recoveryId,
                OccurredAt = occurredAt.ToString("O"),
                PayloadJson = payloadJson
            },
            cancellationToken: ct);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }
}

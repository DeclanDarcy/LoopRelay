using Dapper;
using LoopRelay.Persistence.Sqlite.Abstractions;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Persistence.Sqlite;

/// <summary>
/// Dapper-backed <see cref="IRecoveryLedgerStore"/> over the global <c>recovery_ledger</c> table
/// (refactor-lazy-sqlite.md, "Global DB"). Execution orphan reconciliation is a single global state-fix (it loads
/// one global session file), so the claim is recorded under a fixed sentinel <c>repository_id</c> rather than per
/// repo. The claim is atomic: an <c>INSERT ... ON CONFLICT DO NOTHING</c> followed by reading back the stored
/// stamp guarantees exactly one process-lifetime caller observes a successful claim even under concurrent
/// <c>StartedAsync</c> racers and WAL <c>busy_timeout</c> coalescing.
/// </summary>
public sealed class SqliteRecoveryLedgerStore : IRecoveryLedgerStore
{
    /// <summary>
    /// The fixed key for the process-global execution-recovery slot. Execution recovery is not per-repo, so a
    /// single deterministic sentinel row carries its once-per-process stamp.
    /// </summary>
    private const string ExecutionRecoverySentinel = "__execution_recovery__";

    private readonly ISqliteConnectionFactory connectionFactory;

    public SqliteRecoveryLedgerStore(ISqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<bool> TryClaimExecutionRecoveryAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenGlobalConnectionAsync(ct).ConfigureAwait(false);

        // Insert the sentinel row only if it does not yet exist; the inserted-vs-existing decision is then read
        // back as the stored stamp. changes() reports rows affected by the immediately-preceding INSERT, so a
        // value of 1 means THIS call won the claim.
        var command = new CommandDefinition(
            """
            INSERT INTO recovery_ledger (repository_id, execution_recovered_at)
            VALUES (@Sentinel, @Now)
            ON CONFLICT (repository_id) DO NOTHING;
            SELECT changes();
            """,
            new { Sentinel = ExecutionRecoverySentinel, Now = now.ToString("O") },
            cancellationToken: ct);

        long inserted = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task<bool> HasExecutionRecoveredAsync(CancellationToken ct)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenGlobalConnectionAsync(ct).ConfigureAwait(false);

        var command = new CommandDefinition(
            """
            SELECT execution_recovered_at
            FROM recovery_ledger
            WHERE repository_id = @Sentinel;
            """,
            new { Sentinel = ExecutionRecoverySentinel },
            cancellationToken: ct);

        string? stamp = await connection.ExecuteScalarAsync<string?>(command).ConfigureAwait(false);
        return !string.IsNullOrEmpty(stamp);
    }
}

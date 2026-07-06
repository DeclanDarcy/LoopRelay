namespace LoopRelay.Persistence.Sqlite.Abstractions;

/// <summary>
/// The recovery coordination ledger over the global <c>command-center.db</c> (refactor-lazy-sqlite.md,
/// "Global DB"). One row per repo; survives per-repo dir churn. It records the once-per-process correctness
/// state-fixes — chiefly <c>execution_recovered_at</c>, the stamp guarding execution orphan reconciliation so it
/// runs at most once per process after Kestrel binds.
/// </summary>
public interface IRecoveryLedgerStore
{
    /// <summary>
    /// Atomically claims the execution-recovery slot: returns <see langword="true"/> exactly once (the first
    /// caller stamps <c>execution_recovered_at = now</c>), and <see langword="false"/> on every subsequent call.
    /// The claim is global (a single sentinel row), because execution orphan reconciliation loads ONE global
    /// session file and must run once per process regardless of repository count.
    /// </summary>
    Task<bool> TryClaimExecutionRecoveryAsync(DateTimeOffset now, CancellationToken ct);

    /// <summary>True once execution recovery has been stamped for this process/DB.</summary>
    Task<bool> HasExecutionRecoveredAsync(CancellationToken ct);
}

using LoopRelay.Core.Repositories;

namespace LoopRelay.Persistence.Sqlite.Abstractions;

/// <summary>
/// The append-only decision-session recovery-result audit, backed by the per-repo
/// <c>recovery_result</c> table (refactor-lazy-sqlite.md, Phase 4). It replaces the
/// <c>.agents/decision-sessions/recovery/*.json</c> file plane for these rows: the payload is stored as a
/// JSON column round-tripped through the SAME <c>DecisionSessionJson.Options</c> the file path used, so the
/// <c>DecisionSessionRecoveryResult</c> wire shape is preserved by construction. The audit cadence is unchanged
/// — a row is written only on an explicit, state-changing recovery (<c>POST /recovery</c>), never on a read.
/// </summary>
/// <typeparam name="TResult">The recovery-result envelope type the caller serializes (kept generic so the
/// Persistence.Sqlite project does not take a reference on the DecisionSessions domain types).</typeparam>
public interface IRecoveryResultStore
{
    /// <summary>
    /// Returns every persisted recovery-result row for <paramref name="repo"/>, ordered by
    /// <c>occurred_at</c> ascending then <c>recovery_id</c> ordinal — matching the file path's ordering so
    /// <c>GetHistoryAsync</c> is shape- and order-identical.
    /// </summary>
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(Repository repo, CancellationToken ct);

    /// <summary>
    /// Atomically upserts a single recovery-result row keyed by <paramref name="recoveryId"/>
    /// (last-writer-wins per <c>(repository_id, recovery_id)</c>), stamping <paramref name="occurredAt"/>.
    /// </summary>
    Task WriteAsync<TResult>(
        Repository repo, string recoveryId, DateTimeOffset occurredAt, TResult result, CancellationToken ct);
}

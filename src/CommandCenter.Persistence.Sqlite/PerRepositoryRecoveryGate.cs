using System.Collections.Concurrent;
using CommandCenter.Persistence.Sqlite.Abstractions;

namespace CommandCenter.Persistence.Sqlite;

/// <summary>
/// The per-repo SERIALIZER the Derivation Cache refactor (refactor-lazy-sqlite.md, "On-demand recovery design")
/// uses to make concurrent same-repo recovery calls safe. <see cref="DecisionSessionRecoveryService"/> and
/// <see cref="WorkflowRecoveryService"/> have no internal lock — two racing first-accesses could both run
/// recovery and tear a snapshot via naive truncate-then-write. A per-<c>(scope, repo)</c>
/// <see cref="SemaphoreSlim"/> (with concurrency 1) serializes same-repo callers; different repositories and
/// different scopes run concurrently.
///
/// <para>
/// Crucially this is NOT a once-per-process cache: every call RE-RUNS the operation and returns the fresh result
/// of THIS invocation. The served endpoints are live reads — a second <c>GET /workflow/history</c> (or
/// <c>/recovery</c>) must reflect current state, so caching the first result would serve stale data. The earlier
/// <c>Lazy&lt;Task&gt;</c>-per-process design did exactly that and was therefore wrong for the live-read
/// endpoints; the serializer keeps the concurrency safety while preserving freshness.
/// </para>
///
/// <para>
/// The per-key semaphores live in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by
/// <c>(scope, repositoryId)</c>. They are intentionally retained for the process lifetime (one tiny semaphore per
/// active repo/scope) rather than reference-counted and evicted — eviction would re-introduce a race window where
/// two callers could observe different semaphore instances for the same key and run concurrently.
/// </para>
/// </summary>
public sealed class PerRepositoryRecoveryGate : IPerRepositoryRecoveryGate
{
    private readonly ConcurrentDictionary<(string Scope, Guid RepositoryId), SemaphoreSlim> locks = new();

    public async Task<T> RunAsync<T>(
        string scope, Guid repositoryId, Func<CancellationToken, Task<T>> op, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentNullException.ThrowIfNull(op);

        SemaphoreSlim gate = locks.GetOrAdd((scope, repositoryId), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await op(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RunAsync(
        string scope, Guid repositoryId, Func<CancellationToken, Task> op, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(op);
        await RunAsync<object?>(
            scope,
            repositoryId,
            async ct =>
            {
                await op(ct).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }
}

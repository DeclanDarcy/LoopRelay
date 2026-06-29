namespace CommandCenter.Persistence.Sqlite.Abstractions;

/// <summary>
/// Serializes concurrent recovery operations for the same repository so that two same-repo first-requests cannot
/// race the underlying service (which has no internal lock) and tear a snapshot via naive truncate-then-write
/// (refactor-lazy-sqlite.md, "Concurrency"). It is a per-<c>(scope, repo)</c> SERIALIZER, NOT a once-per-process
/// cache: every call RE-RUNS the operation and returns its fresh result, because the served endpoints
/// (<c>GET /workflow/history</c>, <c>GET/POST /recovery</c>) are live reads whose second call must reflect
/// current state. Different repositories run concurrently; same-repo calls queue behind a per-repo lock. The
/// <paramref name="scope"/> separates independent recovery families (workflow recovery vs decision-session
/// recovery) so they do not contend on a single lock.
/// </summary>
public interface IPerRepositoryRecoveryGate
{
    /// <summary>
    /// Runs <paramref name="op"/> under the per-<c>(scope, repo)</c> lock, serializing concurrent same-repo calls
    /// and returning the freshly-computed result of THIS invocation (no caching across calls).
    /// </summary>
    Task<T> RunAsync<T>(
        string scope, Guid repositoryId, Func<CancellationToken, Task<T>> op, CancellationToken cancellationToken);

    /// <summary>
    /// Result-free overload for fire-and-forget recovery operations. Serializes and re-runs identically.
    /// </summary>
    Task RunAsync(
        string scope, Guid repositoryId, Func<CancellationToken, Task> op, CancellationToken cancellationToken);
}

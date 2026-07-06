namespace LoopRelay.Persistence.Sqlite.Abstractions;

/// <summary>
/// The cache primitive for derived data. Generic over the SOURCE-PURE base type: callers store
/// only the time-independent base record here and recompute every clock-dependent field at read
/// time from <c>base + TimeProvider.GetUtcNow()</c>. A cached row is keyed by
/// <c>(repo, kind, fingerprint, formulaVersion)</c>; a mismatch on either fingerprint or
/// formula version is a miss (the cache is busted), which is how source changes and formula
/// bumps invalidate without an explicit delete.
/// </summary>
public interface IDerivedSnapshotCache
{
    /// <summary>
    /// Returns the cached base of type <typeparamref name="T"/> for <paramref name="repo"/>/<paramref name="kind"/>
    /// iff a row exists whose stored <paramref name="fingerprint"/> and <paramref name="formulaVersion"/>
    /// match. Returns <see langword="null"/> on any miss (no row, cross-repo, stale fingerprint, or
    /// bumped formula version). Never throws on a miss.
    /// </summary>
    Task<T?> TryGetAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, CancellationToken ct);

    /// <summary>
    /// Atomically upserts the source-pure <paramref name="baseValue"/> for
    /// <paramref name="repo"/>/<paramref name="kind"/>, stamping the supplied <paramref name="fingerprint"/>
    /// and <paramref name="formulaVersion"/>. Last-writer-wins per <c>(repo, kind)</c>.
    /// </summary>
    Task PutAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, T baseValue, CancellationToken ct);
}

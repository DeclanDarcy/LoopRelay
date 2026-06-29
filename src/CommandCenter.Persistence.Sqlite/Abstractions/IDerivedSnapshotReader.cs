using CommandCenter.Core.Repositories;

namespace CommandCenter.Persistence.Sqlite.Abstractions;

/// <summary>
/// The compute-if-stale-else-cached read seam every derived service shares. It caches the SOURCE-PURE
/// base (keyed by a per-family content fingerprint + formula version, behind a per-<c>(repo, kind)</c>
/// coalescing gate) and recomputes every TIME-DEPENDENT field on each read via the supplied
/// <paramref name="project"/> from <c>base + now</c>. See <see cref="DerivedSnapshotReader"/>.
/// </summary>
public interface IDerivedSnapshotReader
{
    /// <summary>
    /// Returns the live wire record for <paramref name="repo"/>/<paramref name="kind"/>: the source-pure
    /// base is read from cache (or computed once via <paramref name="computeBase"/> and stored, keyed by a
    /// content fingerprint over <paramref name="families"/> + <paramref name="formulaVersion"/>), then the
    /// time-dependent fields are recomputed by <paramref name="project"/> against a fresh clock read.
    /// </summary>
    Task<TLive> ReadDerivedAsync<TBase, TLive>(
        Repository repo,
        string kind,
        IReadOnlyList<SourceFamily> families,
        string formulaVersion,
        Func<CancellationToken, Task<TBase>> computeBase,
        Func<TBase, DateTimeOffset, TLive> project,
        CancellationToken ct)
        where TBase : notnull;
}

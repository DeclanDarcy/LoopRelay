using System.Collections.Concurrent;
using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite.Abstractions;

namespace CommandCenter.Persistence.Sqlite;

/// <summary>
/// The reusable compute-if-stale-else-cached envelope (the design's <c>ReadDerivedAsync&lt;TBase,TLive&gt;</c>).
/// Every derived service routes its read through this helper so the split between the SOURCE-PURE base
/// (cached, keyed by a per-family content fingerprint + formula version) and the TIME-DEPENDENT projection
/// (recomputed on every read from <c>base + TimeProvider.GetUtcNow()</c>) is structural, not incidental.
///
/// A per-<c>(repo, kind)</c> coalescing gate ensures racing first-readers compute the base exactly once:
/// the first caller through the gate computes-and-caches; concurrent callers block on the gate, then find
/// the freshly-cached base on the double-checked read. The cache itself busts on a fingerprint or formula
/// shift (a miss), so source changes invalidate without an explicit delete; nothing time-dependent is ever
/// stored, so a frozen <c>now</c> can never be served.
/// </summary>
public sealed class DerivedSnapshotReader : IDerivedSnapshotReader
{
    private readonly IDerivedSnapshotCache cache;
    private readonly ISourceFingerprintProvider fingerprints;
    private readonly TimeProvider timeProvider;

    private readonly ConcurrentDictionary<(Guid Repo, string Kind), SemaphoreSlim> gates = new();

    public DerivedSnapshotReader(
        IDerivedSnapshotCache cache,
        ISourceFingerprintProvider fingerprints,
        TimeProvider timeProvider)
    {
        this.cache = cache;
        this.fingerprints = fingerprints;
        this.timeProvider = timeProvider;
    }

    public async Task<TLive> ReadDerivedAsync<TBase, TLive>(
        Repository repo,
        string kind,
        IReadOnlyList<SourceFamily> families,
        string formulaVersion,
        Func<CancellationToken, Task<TBase>> computeBase,
        Func<TBase, DateTimeOffset, TLive> project,
        CancellationToken ct)
        where TBase : notnull
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(computeBase);
        ArgumentNullException.ThrowIfNull(project);

        string fingerprint = await fingerprints.ForFamiliesAsync(repo, families, ct).ConfigureAwait(false);

        TBase? cached = await cache.TryGetAsync<TBase>(repo.Id, kind, fingerprint, formulaVersion, ct)
            .ConfigureAwait(false);
        if (cached is null)
        {
            SemaphoreSlim gate = gates.GetOrAdd((repo.Id, kind), _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Double-check after gate acquisition so concurrent first-readers coalesce to one compute.
                cached = await cache.TryGetAsync<TBase>(repo.Id, kind, fingerprint, formulaVersion, ct)
                    .ConfigureAwait(false);
                if (cached is null)
                {
                    cached = await computeBase(ct).ConfigureAwait(false);
                    await cache.PutAsync(repo.Id, kind, fingerprint, formulaVersion, cached, ct)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        // The time-dependent fields are ALWAYS recomputed from the cached pure base + a fresh clock read.
        return project(cached, timeProvider.GetUtcNow());
    }
}

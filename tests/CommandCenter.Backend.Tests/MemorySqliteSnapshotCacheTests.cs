using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Persistence.Sqlite;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// Unit tests for the <see cref="MemorySqliteSnapshotCache"/> test double. These exercise the
/// invalidation semantics every consumer relies on (fingerprint + formula-version gating) without a
/// real DB, so they stay fully parallel OUTSIDE the ProcessEnvironment collection.
/// </summary>
public sealed class MemorySqliteSnapshotCacheTests
{
    private sealed record Base(int Count, string Label);

    [Fact]
    public async Task TryGet_ReturnsNull_BeforeAnyPut()
    {
        var cache = new MemorySqliteSnapshotCache();

        Base? result = await cache.TryGetAsync<Base>(
            Guid.NewGuid(), "metrics-base", "fp", "v1", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PutThenGet_RoundTripsTheBase_WhenFingerprintAndFormulaMatch()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repo = Guid.NewGuid();
        var stored = new Base(7, "metrics");

        await cache.PutAsync(repo, "metrics-base", "fp-1", "v1", stored, CancellationToken.None);
        Base? roundTripped = await cache.TryGetAsync<Base>(
            repo, "metrics-base", "fp-1", "v1", CancellationToken.None);

        Assert.Equal(stored, roundTripped);
    }

    [Fact]
    public async Task TryGet_Misses_WhenFingerprintDiffers()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repo = Guid.NewGuid();
        await cache.PutAsync(repo, "metrics-base", "fp-1", "v1", new Base(1, "a"), CancellationToken.None);

        Base? stale = await cache.TryGetAsync<Base>(
            repo, "metrics-base", "fp-2", "v1", CancellationToken.None);

        Assert.Null(stale);
    }

    [Fact]
    public async Task TryGet_Misses_WhenFormulaVersionDiffers()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repo = Guid.NewGuid();
        await cache.PutAsync(repo, "metrics-base", "fp-1", "v1", new Base(1, "a"), CancellationToken.None);

        Base? busted = await cache.TryGetAsync<Base>(
            repo, "metrics-base", "fp-1", "v2", CancellationToken.None);

        Assert.Null(busted);
    }

    [Fact]
    public async Task TryGet_IsScopedByRepository()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repoA = Guid.NewGuid();
        Guid repoB = Guid.NewGuid();
        await cache.PutAsync(repoA, "metrics-base", "fp", "v1", new Base(1, "a"), CancellationToken.None);

        Base? crossRepo = await cache.TryGetAsync<Base>(
            repoB, "metrics-base", "fp", "v1", CancellationToken.None);

        Assert.Null(crossRepo);
    }

    [Fact]
    public async Task TryGet_IsScopedByKind()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repo = Guid.NewGuid();
        await cache.PutAsync(repo, "metrics-base", "fp", "v1", new Base(1, "a"), CancellationToken.None);

        Base? otherKind = await cache.TryGetAsync<Base>(
            repo, "economics-base", "fp", "v1", CancellationToken.None);

        Assert.Null(otherKind);
    }

    [Fact]
    public async Task Put_OverwritesPriorEntry_ForSameRepoAndKind()
    {
        var cache = new MemorySqliteSnapshotCache();
        Guid repo = Guid.NewGuid();
        await cache.PutAsync(repo, "metrics-base", "fp-1", "v1", new Base(1, "old"), CancellationToken.None);
        await cache.PutAsync(repo, "metrics-base", "fp-2", "v1", new Base(2, "new"), CancellationToken.None);

        Base? latest = await cache.TryGetAsync<Base>(
            repo, "metrics-base", "fp-2", "v1", CancellationToken.None);
        Base? oldFingerprint = await cache.TryGetAsync<Base>(
            repo, "metrics-base", "fp-1", "v1", CancellationToken.None);

        Assert.Equal(new Base(2, "new"), latest);
        Assert.Null(oldFingerprint);
    }
}

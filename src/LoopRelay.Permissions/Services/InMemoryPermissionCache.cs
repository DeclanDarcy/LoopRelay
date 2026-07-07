using System.Collections.Concurrent;
using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class InMemoryPermissionCache : IPermissionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.Ordinal);

    public bool TryGet(string fingerprint, out CacheEntry entry) =>
        cache.TryGetValue(fingerprint, out entry);

    public void Set(string fingerprint, CacheEntry entry) =>
        cache[fingerprint] = entry;

    public void Clear() => cache.Clear();
}

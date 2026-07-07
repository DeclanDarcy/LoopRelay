using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionCache
{
    bool TryGet(string fingerprint, out CacheEntry entry);

    void Set(string fingerprint, CacheEntry entry);

    void Clear();
}

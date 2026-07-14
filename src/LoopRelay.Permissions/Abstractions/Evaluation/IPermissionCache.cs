using LoopRelay.Permissions.Primitives.Evaluation;

namespace LoopRelay.Permissions.Abstractions.Evaluation;

public interface IPermissionCache
{
    bool TryGet(string fingerprint, out CacheEntry entry);

    void Set(string fingerprint, CacheEntry entry);

    void Clear();
}

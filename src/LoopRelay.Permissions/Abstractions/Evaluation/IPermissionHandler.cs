using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionHandler
{
    PermissionResult Evaluate(PermissionRequest request);

    void ClearCache();
}

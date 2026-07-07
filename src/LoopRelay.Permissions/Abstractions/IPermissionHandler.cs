using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionHandler
{
    PermissionResult Evaluate(PermissionRequest request);

    void ClearCache();
}

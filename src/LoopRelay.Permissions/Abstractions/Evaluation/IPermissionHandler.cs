using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Abstractions.Evaluation;

public interface IPermissionHandler
{
    PermissionResult Evaluate(PermissionRequest request);

    void ClearCache();
}

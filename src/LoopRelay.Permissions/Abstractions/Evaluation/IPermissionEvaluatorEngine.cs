using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionEvaluatorEngine
{
    EvalResult Evaluate(CanonicalCommand[] commands);
}

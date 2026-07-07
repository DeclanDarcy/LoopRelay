using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionEvaluatorEngine
{
    EvalResult Evaluate(CanonicalCommand[] commands);
}

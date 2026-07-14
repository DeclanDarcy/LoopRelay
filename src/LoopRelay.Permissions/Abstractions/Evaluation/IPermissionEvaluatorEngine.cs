using LoopRelay.Permissions.Primitives.Evaluation;
using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Abstractions.Evaluation;

public interface IPermissionEvaluatorEngine
{
    EvalResult Evaluate(CanonicalCommand[] commands);
}

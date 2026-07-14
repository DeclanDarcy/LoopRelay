using LoopRelay.Permissions.Primitives.Evaluation;
using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Abstractions.Security;

public interface IInvariantGuard
{
    EvalResult Enforce(CanonicalCommand[] commands, EvalResult result);
}

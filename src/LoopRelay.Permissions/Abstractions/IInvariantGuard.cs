using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface IInvariantGuard
{
    EvalResult Enforce(CanonicalCommand[] commands, EvalResult result);
}

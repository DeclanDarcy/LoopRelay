using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface IInvariantGuard
{
    EvalResult Enforce(CanonicalCommand[] commands, EvalResult result);
}

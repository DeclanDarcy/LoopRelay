using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface ICommandCanonicalizer
{
    CanonicalCommand[] Canonicalize(ParsedCommand[] commands);
}

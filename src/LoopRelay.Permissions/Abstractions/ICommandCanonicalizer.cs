using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface ICommandCanonicalizer
{
    CanonicalCommand[] Canonicalize(ParsedCommand[] commands);
}

using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Abstractions.Parsing;

public interface ICommandCanonicalizer
{
    CanonicalCommand[] Canonicalize(ParsedCommand[] commands);
}

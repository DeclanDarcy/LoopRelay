using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Abstractions.Parsing;

public interface ICommandParser
{
    ParseResult Parse(string toolName, string? rawCommand);
}

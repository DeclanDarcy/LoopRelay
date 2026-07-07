using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface ICommandParser
{
    ParseResult Parse(string toolName, string? rawCommand);
}

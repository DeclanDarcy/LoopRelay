using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface ICommandParser
{
    ParseResult Parse(string toolName, string? rawCommand);
}

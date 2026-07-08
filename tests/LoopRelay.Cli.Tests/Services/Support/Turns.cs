using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Cli.Tests.Services.Support;

internal static class Turns
{
    public static AgentTurnResult Completed(string output) =>
        new(0, AgentTurnState.Completed, output, new AgentTokenUsage(0, 0));

    public static AgentTurnResult Failed(string output = "boom", string? diagnostics = null) =>
        new(0, AgentTurnState.Failed, output, new AgentTokenUsage(0, 0), diagnostics);
}

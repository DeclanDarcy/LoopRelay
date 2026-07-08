using System.Collections.Concurrent;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Projections;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;

namespace LoopRelay.Plan.Cli.Tests;

internal static class Turns
{
    public static AgentTurnResult Completed(string output) =>
        new(0, AgentTurnState.Completed, output, new AgentTokenUsage(0, 0));

    public static AgentTurnResult Failed(string output = "boom", string? diagnostics = null) =>
        new(0, AgentTurnState.Failed, output, new AgentTokenUsage(0, 0), diagnostics);
}

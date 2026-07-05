using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Roadmap.Cli;

internal interface IRoadmapExecutionBridge
{
    Task<RoadmapExecutionBridgeResult> RunAsync(CancellationToken cancellationToken);
}

internal sealed class RoadmapExecutionBridge(
    IAgentRuntime runtime,
    RoadmapArtifacts artifacts,
    Repository repository,
    ILoopConsole console) : IRoadmapExecutionBridge
{
    public async Task<RoadmapExecutionBridgeResult> RunAsync(CancellationToken cancellationToken)
    {
        string prompt = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ExecutionPrompt);
        var renderer = new ConsoleTurnRenderer(console);
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.ExecutionBridge(repository),
            prompt,
            renderer.Stream,
            cancellationToken);

        renderer.EchoIfSilent(result.Output);
        return result.State == AgentTurnState.Completed
            ? RoadmapExecutionBridgeResult.Completed(result.Output)
            : RoadmapExecutionBridgeResult.Failed(result.Diagnostics ?? $"Execution bridge ended in state {result.State}.");
    }
}

internal sealed record RoadmapExecutionBridgeResult(bool EpicCompleted, bool Blocked, string Message)
{
    public static RoadmapExecutionBridgeResult Completed(string message) => new(true, false, message);

    public static RoadmapExecutionBridgeResult BlockedResult(string message) => new(false, true, message);

    public static RoadmapExecutionBridgeResult Failed(string message) => new(false, false, message);
}

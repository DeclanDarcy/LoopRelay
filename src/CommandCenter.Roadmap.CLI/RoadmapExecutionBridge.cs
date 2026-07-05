using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Roadmap.Cli;

internal interface IRoadmapExecutionBridge
{
    Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken);
}

internal sealed class RoadmapExecutionBridge(
    IAgentRuntime runtime,
    RoadmapArtifacts artifacts,
    Repository repository,
    ILoopConsole console) : IRoadmapExecutionBridge
{
    public async Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken)
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
            ? RoadmapExecutionTransportResult.Completed(result.Output)
            : RoadmapExecutionTransportResult.Failed(
                result.State.ToString(),
                result.Diagnostics ?? $"Execution bridge ended in state {result.State}.",
                result.Output);
    }
}

internal sealed record RoadmapExecutionTransportResult(
    ExecutionTransportStatus Status,
    string AgentState,
    string Output,
    string Diagnostics)
{
    public static RoadmapExecutionTransportResult Completed(string output) =>
        new(ExecutionTransportStatus.Completed, AgentTurnState.Completed.ToString(), output, string.Empty);

    public static RoadmapExecutionTransportResult Failed(string agentState, string diagnostics, string output = "") =>
        new(ExecutionTransportStatus.Failed, agentState, output, diagnostics);
}

internal enum ExecutionTransportStatus
{
    Completed,
    Failed,
}

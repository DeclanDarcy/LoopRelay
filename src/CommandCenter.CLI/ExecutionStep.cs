using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// One execution codex turn. Mirrors RepositoryOrchestrator.RunExecutionAsync/RunContinuationAsync:
/// render Start/ContinueExecution, run a one-shot workspace-write Medium turn, print the assistant
/// message, then verify the agent wrote a new .agents/handoffs/handoff.md.
/// </summary>
internal sealed class ExecutionStep(
    IAgentRuntime runtime, LoopArtifacts artifacts, ILoopConsole console, Repository repository)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string? plan = await artifacts.ReadPlanAsync();
        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        (string? decisions, _) = await artifacts.ReadLatestDecisionsAsync();

        bool continuing = handoff is not null;
        string phase = continuing ? "ContinueExecution" : "StartExecution";
        string prompt = continuing
            ? ContinueExecution.Render(plan, handoff, decisions)
            : StartExecution.Render(plan);

        console.Phase($"Execution: {phase}");
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.Medium, identifier: null),
            prompt,
            StreamToConsole,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new LoopStepException($"Execution turn ended in state {result.State}.");
        }

        console.Message(result.Output);

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff))
        {
            throw new LoopStepException(
                "Execution completed but .agents/handoffs/handoff.md was not written.");
        }

        console.Info("New handoff.md verified.");
    }

    private Task StreamToConsole(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput)
        {
            console.Delta(chunk.Content);
        }

        return Task.CompletedTask;
    }
}

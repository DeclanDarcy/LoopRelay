using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Cli;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Plan.Cli.Services.Agents;

internal sealed class ProjectionPromptRunner(
    IAgentRuntime _runtime,
    Repository _repository,
    ILoopConsole _console) : IProjectionPromptRunner
{
    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var renderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult result = await _runtime.RunOneShotAsync(
            AgentSpecs.Review(_repository),
            prompt,
            renderer.Stream,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new PlanStepException(WithDiagnostics(
                $"{definition.ProjectionPromptName} turn ended in state {result.State}.",
                result.Diagnostics));
        }

        renderer.EchoIfSilent(result.Output);
        return result.Output;
    }

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}

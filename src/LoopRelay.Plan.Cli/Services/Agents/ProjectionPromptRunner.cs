using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models;

namespace LoopRelay.Plan.Cli.Services;

internal sealed class ProjectionPromptRunner(
    IAgentRuntime runtime,
    Repository repository,
    ILoopConsole console) : IProjectionPromptRunner
{
    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var renderer = new ConsoleTurnRenderer(console);
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.Review(repository),
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

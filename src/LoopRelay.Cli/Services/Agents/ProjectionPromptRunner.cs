using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Cli.Services.Agents;

internal sealed class ProjectionPromptRunner(
    IAgentRuntime runtime,
    Repository repository,
    ILoopConsole console) : IProjectionPromptRunner
{
    private readonly IAgentRuntime _runtime = runtime;
    private readonly Repository _repository = repository;
    private readonly ILoopConsole _console = console;
    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var renderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult result = await _runtime.RunOneShotAsync(
            AgentSpecs.Decision(_repository),
            prompt,
            renderer.Stream,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new LoopStepException(WithDiagnostics(
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

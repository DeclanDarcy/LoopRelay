using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Cli.Services.Agents;

internal sealed class ProjectionPromptRunner(
    IAgentRuntime _runtime,
    Repository _repository,
    ILoopConsole _console,
    IRenderedPromptStore? _renderedPromptStore = null,
    string? _policyIdentity = null,
    PromptExecutionContext? _executionContext = null) : IProjectionPromptRunner
{
    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var renderer = new ConsoleTurnRenderer(_console);
        await TryAppendRenderedPromptAsync(definition.ProjectionPromptName, prompt);
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

    // Projection prompts are composed upstream (catalog template + project context); the fact
    // records the exact sent text. Template-hash linkage for runner-shaped sends joins at M7
    // when the runtime gateway owns per-send capture.
    private async Task TryAppendRenderedPromptAsync(string promptIdentity, string renderedText)
    {
        if (_renderedPromptStore is null)
        {
            return;
        }

        try
        {
            await _renderedPromptStore.AppendAsync(
                new RenderedPromptCapture(
                    _executionContext?.TransitionRunId ?? string.Empty,
                    _executionContext?.AttemptId,
                    promptIdentity,
                    null,
                    renderedText,
                    [],
                    _policyIdentity,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            // Rendered-prompt persistence is supporting evidence; failing to append must not fail the projection.
        }
    }

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}

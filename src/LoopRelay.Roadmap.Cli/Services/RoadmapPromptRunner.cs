using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class RoadmapPromptRunner(
    IAgentRuntime runtime,
    Repository repository,
    ILoopConsole console,
    string? promptPolicy = null)
{
    private readonly string promptPolicy = promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault();

    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition projection,
        string projectContext,
        CancellationToken cancellationToken)
    {
        string prompt = projection.RenderPrompt(projectContext);
        return await RunOneShotAsync(projection.ProjectionPromptName, prompt, cancellationToken);
    }

    public async Task<string> RunRuntimePromptAsync(
        string runtimePromptName,
        string projectContext,
        string secondaryInput,
        CancellationToken cancellationToken)
    {
        string prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
            RoadmapPromptCatalog.RenderRuntime(runtimePromptName, projectContext, secondaryInput),
            promptPolicy);
        return await RunOneShotAsync(runtimePromptName, prompt, cancellationToken);
    }

    private async Task<string> RunOneShotAsync(string label, string prompt, CancellationToken cancellationToken)
    {
        var renderer = new ConsoleTurnRenderer(console);
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.ReadOnlyPlanning(repository),
            prompt,
            renderer.Stream,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new RoadmapStepException(WithDiagnostics($"{label} turn ended in state {result.State}.", result.Diagnostics));
        }

        renderer.EchoIfSilent(result.Output);
        return result.Output;
    }

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}

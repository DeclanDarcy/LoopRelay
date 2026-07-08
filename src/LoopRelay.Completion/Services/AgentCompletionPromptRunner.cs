using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Completion;

public sealed class AgentCompletionPromptRunner(
    IAgentRuntime runtime,
    Repository repository,
    string? promptPolicy = null) : ICompletionPromptRunner
{
    private readonly string promptPolicy = promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault();

    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        string prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
            CompletionPromptCatalog.RenderRuntime(invocation),
            promptPolicy);
        AgentSessionSpec spec = string.Equals(
            invocation.RuntimePromptName,
            CompletionRuntimePromptNames.SynthesizeCompletedEpic,
            StringComparison.Ordinal)
            ? WritablePlanning(repository)
            : ReadOnlyPlanning(repository);

        AgentTurnResult result = await runtime.RunOneShotAsync(
            spec,
            prompt,
            onChunk: null,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new CompletionCertificationException(
                WithDiagnostics($"{invocation.RuntimePromptName} turn ended in state {result.State}.", result.Diagnostics));
        }

        return result.Output;
    }

    private static AgentSessionSpec ReadOnlyPlanning(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    private static AgentSessionSpec WritablePlanning(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}

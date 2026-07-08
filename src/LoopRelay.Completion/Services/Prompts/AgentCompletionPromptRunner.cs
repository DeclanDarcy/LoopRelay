using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Completion.Services.Prompts;

public sealed class AgentCompletionPromptRunner(
    IAgentRuntime runtime,
    Repository repository,
    string? promptPolicy = null) : ICompletionPromptRunner
{
    private readonly IAgentRuntime _runtime = runtime;
    private readonly Repository _repository = repository;
    private readonly string _promptPolicy = promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault();

    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        string prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
            CompletionPromptCatalog.RenderRuntime(invocation),
            _promptPolicy);
        AgentSessionSpec spec = string.Equals(
            invocation.RuntimePromptName,
            CompletionRuntimePromptNames.SynthesizeCompletedEpic,
            StringComparison.Ordinal)
            ? WritablePlanning(_repository)
            : ReadOnlyPlanning(_repository);

        AgentTurnResult result = await _runtime.RunOneShotAsync(
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

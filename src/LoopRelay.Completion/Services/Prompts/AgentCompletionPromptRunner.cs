using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Runtime;

namespace LoopRelay.Completion.Services.Prompts;

public sealed class AgentCompletionPromptRunner(
    IAgentRuntime _runtime,
    Repository _repository,
    IRenderedPromptStore? _renderedPromptStore = null,
    string? _policyIdentity = null,
    PromptExecutionContext? _executionContext = null) : ICompletionPromptRunner
{

    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        // The implementation-first prompt policy is template-owned: every completion runtime template
        // carries the section in its hashed source, so nothing is appended after rendering.
        string prompt = CompletionPromptCatalog.RenderRuntime(invocation);
        await TryAppendRenderedPromptAsync(invocation.RuntimePromptName, prompt);
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

    // The invocation's project context and secondary input are fully embedded in the rendered
    // text; the fact records the text and the template's build-time source hash.
    private async Task TryAppendRenderedPromptAsync(string runtimePromptName, string prompt)
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
                    runtimePromptName,
                    CompletionPromptCatalog.TemplateSourceHash(runtimePromptName),
                    prompt,
                    [],
                    _policyIdentity,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            // Rendered-prompt persistence is supporting evidence; failing to append must not fail certification.
        }
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

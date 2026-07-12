using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Completion.Services.Prompts;

/// <summary>
/// Executes completion prompts through Prompt Authority. It never selects provider runtime
/// settings and never materializes the provider response into repository state.
/// </summary>
public sealed class AgentCompletionPromptRunner(
    IPromptDispatchGateway _prompts,
    IPromptComposer _composer,
    PromptPolicyProfile _policyProfile,
    PromptDispatchAuthorization _authorization,
    ConsumedInputManifestIdentity _consumedInputManifest,
    IReadOnlyList<ConsumedInputFile> _consumedInputs) : ICompletionPromptRunner
{
    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        string renderedPrompt = CompletionPromptCatalog.RenderRuntime(invocation);
        var variables = new Dictionary<string, string>
        {
            [nameof(invocation.ProjectContext)] = invocation.ProjectContext,
            [nameof(invocation.SecondaryInput)] = invocation.SecondaryInput,
            [nameof(invocation.Label)] = invocation.Label,
        };
        PromptComposition composition = _composer.Compose(
            new PromptTemplateIdentity(invocation.RuntimePromptName),
            CompletionPromptCatalog.TemplateSourceHash(invocation.RuntimePromptName),
            _authorization.Policy,
            _policyProfile,
            _consumedInputManifest,
            _consumedInputs,
            variables,
            renderedPrompt);

        PreparedPromptDispatch prepared = await _prompts.PrepareAsync(
            composition,
            _authorization,
            cancellationToken);
        PromptExecutionResult result = await _prompts.DispatchAsync(prepared, cancellationToken);
        if (result.Status != PromptExecutionStatus.Completed)
        {
            throw new CompletionCertificationException(
                $"{invocation.RuntimePromptName} turn ended in state {result.Status}." +
                (string.IsNullOrWhiteSpace(result.FailureMessage)
                    ? string.Empty
                    : $" Provider diagnostics: {result.FailureMessage}"));
        }

        return result.RawOutput;
    }
}

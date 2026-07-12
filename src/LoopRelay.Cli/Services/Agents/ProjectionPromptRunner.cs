using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Services.Prompts;

namespace LoopRelay.Cli.Services.Agents;

internal sealed class ProjectionPromptRunner(
    IPromptDispatchGateway _prompts,
    IPromptComposer _composer,
    PromptPolicyProfile _policyProfile,
    PromptDispatchAuthorization _authorization,
    ConsumedInputManifestIdentity _consumedInputManifest,
    IReadOnlyList<ConsumedInputFile> _consumedInputs,
    ILoopConsole _console) : IProjectionPromptRunner
{
    public async Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var metadata = ProjectionPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName);
        PromptComposition composition = _composer.Compose(
            new PromptTemplateIdentity(metadata.PromptName),
            metadata.SourceHash,
            _authorization.Policy,
            _policyProfile,
            _consumedInputManifest,
            _consumedInputs,
            new Dictionary<string, string>(),
            prompt);

        PreparedPromptDispatch prepared = await _prompts.PrepareAsync(
            composition,
            _authorization,
            cancellationToken);
        PromptExecutionResult result = await _prompts.DispatchAsync(prepared, cancellationToken);
        if (result.Status != PromptExecutionStatus.Completed)
        {
            throw new LoopStepException(
                $"{definition.ProjectionPromptName} turn ended in state {result.Status}." +
                (string.IsNullOrWhiteSpace(result.FailureMessage)
                    ? string.Empty
                    : $" Provider diagnostics: {result.FailureMessage}"));
        }

        new ConsoleTurnRenderer(_console).EchoIfSilent(result.RawOutput);
        return result.RawOutput;
    }
}

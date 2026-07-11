using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Completion.Services.Prompts;

public sealed class AgentCompletionPromptRunner(
    IAgentRuntime _runtime,
    Repository _repository,
    BrainConfiguration _brainConfiguration,
    string? _promptPolicy = null) : ICompletionPromptRunner
{

    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        string prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
            CompletionPromptCatalog.RenderRuntime(invocation),
            (_promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault()));
        AgentSessionSpec spec = ReadOnlyPlanning(_repository, _brainConfiguration);

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

        if (string.Equals(
                invocation.RuntimePromptName,
                CompletionRuntimePromptNames.SynthesizeCompletedEpic,
                StringComparison.Ordinal))
        {
            await MaterializeSynthesisFallbackAsync(invocation.Label, result.Output, cancellationToken);
        }

        return result.Output;
    }

    private async Task MaterializeSynthesisFallbackAsync(
        string? label,
        string output,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(label, out int index) || index <= 0)
        {
            throw new CompletionCertificationException("SynthesizeCompletedEpic requires a positive numeric archive label.");
        }

        string relativePath = $".agents/archive/epics/{index}.md";
        string path = ArtifactPath.ResolveRepositoryPath(_repository, relativePath);
        if (File.Exists(path)) return;

        string content = output.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal) &&
            content.EndsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = content.IndexOf('\n');
            content = firstNewline >= 0
                ? content[(firstNewline + 1)..^3].Trim()
                : string.Empty;
        }

        if (!content.StartsWith("# ", StringComparison.Ordinal) ||
            !content.Contains("## 1. Epic Purpose", StringComparison.Ordinal))
        {
            string headings = string.Join(
                " | ",
                content
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => line.StartsWith('#'))
                    .Take(8));
            throw new CompletionCertificationException(
                $"SynthesizeCompletedEpic returned neither its required file nor a structurally valid Markdown synthesis. " +
                $"Output length: {content.Length}; headings: {(headings.Length == 0 ? "none" : headings)}.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content + Environment.NewLine, cancellationToken);
    }

    private static AgentSessionSpec ReadOnlyPlanning(Repository repository, BrainConfiguration brain) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path);

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}

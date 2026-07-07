using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Completion;

public static class CompletionRuntimePromptNames
{
    public const string CreateRoadmapCompletionContext = "CreateRoadmapCompletionContext";
    public const string EvaluateEpicCompletionAndDrift = "EvaluateEpicCompletionAndDrift";
    public const string SynthesizeCompletedEpic = "SynthesizeCompletedEpic";
    public const string UpdateRoadmapCompletionContext = "UpdateRoadmapCompletionContext";
}

public sealed record CompletionRuntimePromptInvocation(
    string RuntimePromptName,
    string ProjectContext = "",
    string SecondaryInput = "",
    string Label = "");

public interface ICompletionPromptRunner
{
    Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default);
}

public static class CompletionPromptCatalog
{
    public static string RenderRuntime(CompletionRuntimePromptInvocation invocation) =>
        invocation.RuntimePromptName switch
        {
            CompletionRuntimePromptNames.CreateRoadmapCompletionContext =>
                Core.Prompts.Planning.CreateRoadmapCompletionContext.Render(
                    invocation.ProjectContext,
                    invocation.SecondaryInput),
            CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift =>
                Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.Render(invocation.ProjectContext),
            CompletionRuntimePromptNames.SynthesizeCompletedEpic =>
                Core.Prompts.Planning.SynthesizeCompletedEpic.Render(invocation.Label),
            CompletionRuntimePromptNames.UpdateRoadmapCompletionContext =>
                Core.Prompts.Planning.UpdateRoadmapCompletionContext.Render(
                    invocation.ProjectContext,
                    invocation.SecondaryInput),
            _ => throw new ArgumentOutOfRangeException(
                nameof(invocation),
                invocation.RuntimePromptName,
                "Unknown completion runtime prompt."),
        };
}

public sealed class AgentCompletionPromptRunner(IAgentRuntime runtime, Repository repository) : ICompletionPromptRunner
{
    public async Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        string prompt = CompletionPromptCatalog.RenderRuntime(invocation);
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

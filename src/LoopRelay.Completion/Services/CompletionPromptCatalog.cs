using LoopRelay.Completion.Models;

namespace LoopRelay.Completion.Services;

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

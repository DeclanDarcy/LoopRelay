using LoopRelay.Completion.Models.Prompts;

namespace LoopRelay.Completion.Services.Prompts;

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

    // The build-time source hash of the template behind a runtime prompt: with policy text
    // template-owned, this hash is the policy-complete prompt version recorded in evidence.
    public static string TemplateSourceHash(string runtimePromptName) =>
        runtimePromptName switch
        {
            CompletionRuntimePromptNames.CreateRoadmapCompletionContext =>
                Core.Prompts.Planning.CreateRoadmapCompletionContext.SourceHash,
            CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift =>
                Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.SourceHash,
            CompletionRuntimePromptNames.SynthesizeCompletedEpic =>
                Core.Prompts.Planning.SynthesizeCompletedEpic.SourceHash,
            CompletionRuntimePromptNames.UpdateRoadmapCompletionContext =>
                Core.Prompts.Planning.UpdateRoadmapCompletionContext.SourceHash,
            _ => throw new ArgumentOutOfRangeException(
                nameof(runtimePromptName),
                runtimePromptName,
                "Unknown completion runtime prompt."),
        };
}

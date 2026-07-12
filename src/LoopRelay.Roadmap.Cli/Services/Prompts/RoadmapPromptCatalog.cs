using LoopRelay.Roadmap.Cli.Models.Projections;

namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal static class RoadmapPromptCatalog
{
    public static ProjectionPromptMetadata GetProjectionMetadata(string projectionPromptName) =>
        projectionPromptName switch
        {
            "ProjectionForCreateRoadmapCompletionContext" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext).FullName,
                    Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext.SourceHash),
            "ProjectionForUpdateRoadmapCompletionContext" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext).FullName,
                    Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext.SourceHash),
            "ProjectionForSelectNextEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForSelectNextEpic).FullName,
                    Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash),
            "ProjectionForEpicPreparationAudit" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForEpicPreparationAudit).FullName,
                    Core.Prompts.Projections.ProjectionForEpicPreparationAudit.SourceHash),
            "ProjectionForRealignEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForRealignEpic).FullName,
                    Core.Prompts.Projections.ProjectionForRealignEpic.SourceHash),
            "ProjectionForReimagineEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForReimagineEpic).FullName,
                    Core.Prompts.Projections.ProjectionForReimagineEpic.SourceHash),
            "ProjectionForCreateNewEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForCreateNewEpic).FullName,
                    Core.Prompts.Projections.ProjectionForCreateNewEpic.SourceHash),
            "ProjectionForSplitEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForSplitEpic).FullName,
                    Core.Prompts.Projections.ProjectionForSplitEpic.SourceHash),
            "ProjectionForGenerateMilestoneDeepDivesForEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic).FullName,
                    Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic.SourceHash),
            "ProjectionForEvaluateEpicCompletionAndDrift" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift).FullName,
                    Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift.SourceHash),
            _ => throw new ArgumentOutOfRangeException(nameof(projectionPromptName), projectionPromptName, "Unknown projection prompt."),
        };

    public static string RenderProjection(string projectionPromptName, string projectContext) =>
        projectionPromptName switch
        {
            "ProjectionForCreateRoadmapCompletionContext" =>
                Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext.Render(projectContext),
            "ProjectionForUpdateRoadmapCompletionContext" =>
                Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext.Render(projectContext),
            "ProjectionForSelectNextEpic" =>
                Core.Prompts.Projections.ProjectionForSelectNextEpic.Render(projectContext),
            "ProjectionForEpicPreparationAudit" =>
                Core.Prompts.Projections.ProjectionForEpicPreparationAudit.Render(projectContext),
            "ProjectionForRealignEpic" =>
                Core.Prompts.Projections.ProjectionForRealignEpic.Render(projectContext),
            "ProjectionForReimagineEpic" =>
                Core.Prompts.Projections.ProjectionForReimagineEpic.Render(projectContext),
            "ProjectionForCreateNewEpic" =>
                Core.Prompts.Projections.ProjectionForCreateNewEpic.Render(projectContext),
            "ProjectionForSplitEpic" =>
                Core.Prompts.Projections.ProjectionForSplitEpic.Render(projectContext),
            "ProjectionForGenerateMilestoneDeepDivesForEpic" =>
                Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic.Render(projectContext),
            "ProjectionForEvaluateEpicCompletionAndDrift" =>
                Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift.Render(projectContext),
            _ => throw new ArgumentOutOfRangeException(nameof(projectionPromptName), projectionPromptName, "Unknown projection prompt."),
        };

    public static string RenderRuntime(
        string runtimePromptName,
        string projectContext,
        string secondaryInput = "")
    {
        return runtimePromptName switch
        {
            "CreateRoadmapCompletionContext" =>
                Core.Prompts.Planning.CreateRoadmapCompletionContext.Render(projectContext, secondaryInput),
            "UpdateRoadmapCompletionContext" =>
                Core.Prompts.Planning.UpdateRoadmapCompletionContext.Render(projectContext, secondaryInput),
            "SelectNextEpic" =>
                Core.Prompts.Planning.SelectNextEpic.Render(projectContext),
            "EpicPreparationAudit" =>
                Core.Prompts.Planning.EpicPreparationAudit.Render(projectContext, secondaryInput),
            "RealignEpic" =>
                Core.Prompts.Planning.RealignEpic.Render(projectContext, secondaryInput),
            "ReimagineEpic" =>
                Core.Prompts.Planning.ReimagineEpic.Render(projectContext, secondaryInput),
            "CreateNewEpic" =>
                Core.Prompts.Planning.CreateNewEpic.Render(projectContext, secondaryInput),
            "SplitEpic" =>
                Core.Prompts.Planning.SplitEpic.Render(projectContext, secondaryInput),
            "GenerateMilestoneDeepDivesForEpic" =>
                Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Render(projectContext),
            "EvaluateEpicCompletionAndDrift" =>
                Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.Render(projectContext),
            "SynthesizeCompletedEpic" =>
                Core.Prompts.Planning.SynthesizeCompletedEpic.Render(secondaryInput),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "Unknown runtime prompt."),
        };
    }

    // The build-time source hash of the template behind a runtime prompt. With the prompt
    // policy template-owned (M6), this hash is the policy-complete prompt version that the
    // transition journal stamps into input snapshots.
    public static string RuntimeTemplateSourceHash(string runtimePromptName) =>
        runtimePromptName switch
        {
            "CreateRoadmapCompletionContext" => Core.Prompts.Planning.CreateRoadmapCompletionContext.SourceHash,
            "UpdateRoadmapCompletionContext" => Core.Prompts.Planning.UpdateRoadmapCompletionContext.SourceHash,
            "SelectNextEpic" => Core.Prompts.Planning.SelectNextEpic.SourceHash,
            "EpicPreparationAudit" => Core.Prompts.Planning.EpicPreparationAudit.SourceHash,
            "RealignEpic" => Core.Prompts.Planning.RealignEpic.SourceHash,
            "ReimagineEpic" => Core.Prompts.Planning.ReimagineEpic.SourceHash,
            "CreateNewEpic" => Core.Prompts.Planning.CreateNewEpic.SourceHash,
            "SplitEpic" => Core.Prompts.Planning.SplitEpic.SourceHash,
            "GenerateMilestoneDeepDivesForEpic" => Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.SourceHash,
            "EvaluateEpicCompletionAndDrift" => Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.SourceHash,
            "SynthesizeCompletedEpic" => Core.Prompts.Planning.SynthesizeCompletedEpic.SourceHash,
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "Unknown runtime prompt."),
        };

    private static ProjectionPromptMetadata Metadata(string promptName, string? promptType, string sourceHash) =>
        new(promptName, promptType ?? promptName, sourceHash);
}

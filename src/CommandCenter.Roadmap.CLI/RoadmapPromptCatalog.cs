namespace CommandCenter.Roadmap.Cli;

internal static class RoadmapPromptCatalog
{
    public static ProjectionPromptMetadata GetProjectionMetadata(string projectionPromptName) =>
        projectionPromptName switch
        {
            "ProjectionForCreateRoadmapCompletionContext" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext.SourceHash),
            "ProjectionForUpdateRoadmapCompletionContext" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext.SourceHash),
            "ProjectionForSelectNextEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash),
            "ProjectionForEpicPreparationAudit" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForEpicPreparationAudit).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForEpicPreparationAudit.SourceHash),
            "ProjectionForRealignEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForRealignEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForRealignEpic.SourceHash),
            "ProjectionForReimagineEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForReimagineEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForReimagineEpic.SourceHash),
            "ProjectionForCreateNewEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForCreateNewEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForCreateNewEpic.SourceHash),
            "ProjectionForSplitEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForSplitEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForSplitEpic.SourceHash),
            "ProjectionForGenerateMilestoneDeepDivesForEpic" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic.SourceHash),
            "ProjectionForEvaluateEpicCompletionAndDrift" =>
                Metadata(
                    projectionPromptName,
                    typeof(CommandCenter.Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift).FullName,
                    CommandCenter.Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift.SourceHash),
            _ => throw new ArgumentOutOfRangeException(nameof(projectionPromptName), projectionPromptName, "Unknown projection prompt."),
        };

    public static string RenderProjection(string projectionPromptName, string projectContext) =>
        projectionPromptName switch
        {
            "ProjectionForCreateRoadmapCompletionContext" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForCreateRoadmapCompletionContext.Render(projectContext),
            "ProjectionForUpdateRoadmapCompletionContext" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForUpdateRoadmapCompletionContext.Render(projectContext),
            "ProjectionForSelectNextEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic.Render(projectContext),
            "ProjectionForEpicPreparationAudit" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForEpicPreparationAudit.Render(projectContext),
            "ProjectionForRealignEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForRealignEpic.Render(projectContext),
            "ProjectionForReimagineEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForReimagineEpic.Render(projectContext),
            "ProjectionForCreateNewEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForCreateNewEpic.Render(projectContext),
            "ProjectionForSplitEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForSplitEpic.Render(projectContext),
            "ProjectionForGenerateMilestoneDeepDivesForEpic" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForGenerateMilestoneDeepDivesForEpic.Render(projectContext),
            "ProjectionForEvaluateEpicCompletionAndDrift" =>
                CommandCenter.Core.Prompts.Projections.ProjectionForEvaluateEpicCompletionAndDrift.Render(projectContext),
            _ => throw new ArgumentOutOfRangeException(nameof(projectionPromptName), projectionPromptName, "Unknown projection prompt."),
        };

    public static string RenderRuntime(string runtimePromptName, string projectContext, string secondaryInput = "") =>
        runtimePromptName switch
        {
            "CreateRoadmapCompletionContext" =>
                CommandCenter.Core.Prompts.Planning.CreateRoadmapCompletionContext.Render(projectContext, secondaryInput),
            "UpdateRoadmapCompletionContext" =>
                CommandCenter.Core.Prompts.Planning.UpdateRoadmapCompletionContext.Render(projectContext),
            "SelectNextEpic" =>
                CommandCenter.Core.Prompts.Planning.SelectNextEpic.Render(projectContext),
            "EpicPreparationAudit" =>
                CommandCenter.Core.Prompts.Planning.EpicPreparationAudit.Render(projectContext, secondaryInput),
            "RealignEpic" =>
                CommandCenter.Core.Prompts.Planning.RealignEpic.Render(projectContext, secondaryInput),
            "ReimagineEpic" =>
                CommandCenter.Core.Prompts.Planning.ReimagineEpic.Render(projectContext, secondaryInput),
            "CreateNewEpic" =>
                CommandCenter.Core.Prompts.Planning.CreateNewEpic.Render(projectContext, secondaryInput),
            "SplitEpic" =>
                CommandCenter.Core.Prompts.Planning.SplitEpic.Render(projectContext, secondaryInput),
            "GenerateMilestoneDeepDivesForEpic" =>
                CommandCenter.Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Render(projectContext),
            "EvaluateEpicCompletionAndDrift" =>
                CommandCenter.Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.Render(projectContext),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "Unknown runtime prompt."),
        };

    private static ProjectionPromptMetadata Metadata(string promptName, string? promptType, string sourceHash) =>
        new(promptName, promptType ?? promptName, sourceHash);
}

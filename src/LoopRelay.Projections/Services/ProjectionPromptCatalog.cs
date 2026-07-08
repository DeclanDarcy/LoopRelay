using LoopRelay.Projections.Models;

namespace LoopRelay.Projections.Services;

public static class ProjectionPromptCatalog
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
            "ProjectionForAdversarialPlanReview" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForAdversarialPlanReview).FullName,
                    Core.Prompts.Projections.ProjectionForAdversarialPlanReview.SourceHash),
            "ProjectionForDecisionSession" =>
                Metadata(
                    projectionPromptName,
                    typeof(Core.Prompts.Projections.ProjectionForDecisionSession).FullName,
                    Core.Prompts.Projections.ProjectionForDecisionSession.SourceHash),
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
            "ProjectionForAdversarialPlanReview" =>
                Core.Prompts.Projections.ProjectionForAdversarialPlanReview.Render(projectContext),
            "ProjectionForDecisionSession" =>
                Core.Prompts.Projections.ProjectionForDecisionSession.Render(projectContext),
            _ => throw new ArgumentOutOfRangeException(nameof(projectionPromptName), projectionPromptName, "Unknown projection prompt."),
        };

    private static ProjectionPromptMetadata Metadata(string promptName, string? promptType, string sourceHash) =>
        new(promptName, promptType ?? promptName, sourceHash);
}

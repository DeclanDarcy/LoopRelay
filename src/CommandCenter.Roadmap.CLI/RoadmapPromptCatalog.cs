namespace CommandCenter.Roadmap.Cli;

internal static class RoadmapPromptCatalog
{
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
}

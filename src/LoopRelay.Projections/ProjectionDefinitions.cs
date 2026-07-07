namespace LoopRelay.Projections;

public sealed class ProjectionDefinitionRegistry
{
    private readonly IReadOnlyDictionary<string, ProjectionDefinition> definitions;

    public ProjectionDefinitionRegistry(IEnumerable<ProjectionDefinition> definitions)
    {
        this.definitions = definitions.ToDictionary(
            definition => definition.RuntimePromptName,
            StringComparer.Ordinal);
    }

    public static ProjectionDefinitionRegistry CreateDefault() =>
        new(
        [
            Define("CreateRoadmapCompletionContext", "ProjectionForCreateRoadmapCompletionContext", "# Roadmap Completion Projection", "CreateRoadmapCompletionContext"),
            Define("UpdateRoadmapCompletionContext", "ProjectionForUpdateRoadmapCompletionContext", "# Roadmap Completion Update Projection", "UpdateRoadmapCompletionContext"),
            Define("SelectNextEpic", "ProjectionForSelectNextEpic", "# Select Next Epic Projection", "SelectNextEpic"),
            Define("EpicPreparationAudit", "ProjectionForEpicPreparationAudit", "# Epic Preparation Audit Projection", "EpicPreparationAudit"),
            Define("RealignEpic", "ProjectionForRealignEpic", "# Epic Realignment Projection", "RealignEpic"),
            Define("ReimagineEpic", "ProjectionForReimagineEpic", "# Epic Reimagination Projection", "ReimagineEpic"),
            Define("CreateNewEpic", "ProjectionForCreateNewEpic", "# Create New Epic Projection", "CreateNewEpic"),
            Define("SplitEpic", "ProjectionForSplitEpic", "# Split Epic Projection", "SplitEpic"),
            Define("GenerateMilestoneDeepDivesForEpic", "ProjectionForGenerateMilestoneDeepDivesForEpic", "# Milestone Deep Dive Projection", "GenerateMilestoneDeepDivesForEpic"),
            Define("EvaluateEpicCompletionAndDrift", "ProjectionForEvaluateEpicCompletionAndDrift", "# Epic Completion Evaluation Projection", "EvaluateEpicCompletionAndDrift"),
            Define(ProjectionRuntimePromptNames.AdversarialPlanReview, "ProjectionForAdversarialPlanReview", "# Adversarial Plan Review Projection", ProjectionRuntimePromptNames.AdversarialPlanReview),
            Define(ProjectionRuntimePromptNames.DecisionSession, "ProjectionForDecisionSession", "# Execution Agent System Prompt Projection", ProjectionRuntimePromptNames.DecisionSession),
        ]);

    public IReadOnlyCollection<ProjectionDefinition> All => definitions.Values.ToList();

    public ProjectionDefinition Get(string runtimePromptName) =>
        definitions.TryGetValue(runtimePromptName, out ProjectionDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "No projection registered for runtime prompt.");

    private static ProjectionDefinition Define(
        string runtimePromptName,
        string projectionPromptName,
        string requiredTitle,
        string intendedConsumer) =>
        new(
            runtimePromptName,
            projectionPromptName,
            ProjectionArtifactPaths.ProjectionPaths[runtimePromptName],
            requiredTitle,
            intendedConsumer);
}

public sealed record ProjectionDefinition(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath,
    string RequiredTitle,
    string IntendedConsumer)
{
    public string RenderPrompt(string projectContext) =>
        ProjectionPromptCatalog.RenderProjection(ProjectionPromptName, projectContext);
}

using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class ProjectionRegistry
{
    private readonly IReadOnlyDictionary<string, ProjectionDefinition> definitions;

    public ProjectionRegistry()
    {
        definitions = new[]
        {
            Define("CreateRoadmapCompletionContext", "ProjectionForCreateRoadmapCompletionContext", RoadmapArtifactPaths.ProjectionPaths["CreateRoadmapCompletionContext"]),
            Define("UpdateRoadmapCompletionContext", "ProjectionForUpdateRoadmapCompletionContext", RoadmapArtifactPaths.ProjectionPaths["UpdateRoadmapCompletionContext"]),
            Define("SelectNextEpic", "ProjectionForSelectNextEpic", RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]),
            Define("EpicPreparationAudit", "ProjectionForEpicPreparationAudit", RoadmapArtifactPaths.ProjectionPaths["EpicPreparationAudit"]),
            Define("RealignEpic", "ProjectionForRealignEpic", RoadmapArtifactPaths.ProjectionPaths["RealignEpic"]),
            Define("ReimagineEpic", "ProjectionForReimagineEpic", RoadmapArtifactPaths.ProjectionPaths["ReimagineEpic"]),
            Define("CreateNewEpic", "ProjectionForCreateNewEpic", RoadmapArtifactPaths.ProjectionPaths["CreateNewEpic"]),
            Define("SplitEpic", "ProjectionForSplitEpic", RoadmapArtifactPaths.ProjectionPaths["SplitEpic"]),
            Define("GenerateMilestoneDeepDivesForEpic", "ProjectionForGenerateMilestoneDeepDivesForEpic", RoadmapArtifactPaths.ProjectionPaths["GenerateMilestoneDeepDivesForEpic"]),
            Define("EvaluateEpicCompletionAndDrift", "ProjectionForEvaluateEpicCompletionAndDrift", RoadmapArtifactPaths.ProjectionPaths["EvaluateEpicCompletionAndDrift"]),
        }.ToDictionary(definition => definition.RuntimePromptName, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<ProjectionDefinition> All => definitions.Values.ToList();

    public ProjectionDefinition Get(string runtimePromptName) =>
        definitions.TryGetValue(runtimePromptName, out ProjectionDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "No projection registered for runtime prompt.");

    private static ProjectionDefinition Define(string runtimePromptName, string projectionPromptName, string path) =>
        new(runtimePromptName, projectionPromptName, path);
}

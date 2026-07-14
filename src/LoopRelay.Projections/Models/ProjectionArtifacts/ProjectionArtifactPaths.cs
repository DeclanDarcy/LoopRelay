using LoopRelay.Core.Services.ProjectContext;

namespace LoopRelay.Projections.Models.ProjectionArtifacts;

public static class ProjectionArtifactPaths
{
    public const string ProjectionsManifest = ".agents/projections/manifest.md";
    public const string ProjectionsManifestJson = ".agents/projections/manifest.json";
    public const string ProjectContextDirectory = ProjectContextSourceContract.DirectoryPath;

    public static readonly IReadOnlyList<string> ProjectContextSourceFiles =
        ProjectContextSourceContract.SourceFiles;

    public static readonly IReadOnlyDictionary<string, string> ProjectionPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CreateRoadmapCompletionContext"] = ".agents/projections/roadmap-completion.md",
            ["UpdateRoadmapCompletionContext"] = ".agents/projections/roadmap-completion-update.md",
            ["SelectNextEpic"] = ".agents/projections/select-next-epic.md",
            ["EpicPreparationAudit"] = ".agents/projections/epic-preparation-audit.md",
            ["RealignEpic"] = ".agents/projections/realign-epic.md",
            ["ReimagineEpic"] = ".agents/projections/reimagine-epic.md",
            ["CreateNewEpic"] = ".agents/projections/create-new-epic.md",
            ["SplitEpic"] = ".agents/projections/split-epic.md",
            ["GenerateMilestoneDeepDivesForEpic"] = ".agents/projections/milestone-deep-dive.md",
            ["EvaluateEpicCompletionAndDrift"] = ".agents/projections/epic-completion-evaluation.md",
            [ProjectionRuntimePromptNames.AdversarialPlanReview] = ".agents/projections/adversarial-plan-review.md",
            [ProjectionRuntimePromptNames.DecisionSession] = ".agents/projections/decision-session.md",
        };
}

namespace LoopRelay.Orchestration.Services;

public static class EvaluationArtifactPaths
{
    public const string InputDirectory = OrchestrationArtifactPaths.AgentsDirectory + "/evals";

    public const string SelectedEvaluation = OrchestrationArtifactPaths.AgentsDirectory + "/selected-evaluation.md";

    public const string DependencyInventory = OrchestrationArtifactPaths.AgentsDirectory + "/eval-dependency-inventory.md";

    public const string HypothesisInventory = OrchestrationArtifactPaths.AgentsDirectory + "/eval-hypothesis-inventory.md";

    public const string ArchitecturalCatalog = OrchestrationArtifactPaths.AgentsDirectory + "/eval-architectural-catalog.md";

    public const string EvalDag = OrchestrationArtifactPaths.AgentsDirectory + "/eval-dag.md";

    public const string NextEpicRoadmap = OrchestrationArtifactPaths.AgentsDirectory + "/next-epic-roadmap.md";

    public const string PreparedEpic = OrchestrationArtifactPaths.AgentsDirectory + "/epic.md";

    public const string MilestoneSpecificationDirectory = OrchestrationArtifactPaths.SpecsDirectory;

    public const string MilestoneSpecificationPattern = "*.md";

    public const string EvidenceDirectory = OrchestrationArtifactPaths.EvidenceDirectory + "/eval";
}

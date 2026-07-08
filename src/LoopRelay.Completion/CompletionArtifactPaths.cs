using LoopRelay.Orchestration;

namespace LoopRelay.Completion;

public static class CompletionArtifactPaths
{
    public const string AgentsDirectory = OrchestrationArtifactPaths.AgentsDirectory;
    public const string ActiveEpic = ".agents/epic.md";
    public const string RoadmapCompletionContext = ".agents/core/roadmap-completion-context.md";
    public const string ExecutionPlan = OrchestrationArtifactPaths.Plan;
    public const string Details = OrchestrationArtifactPaths.Details;
    public const string OperationalContext = OrchestrationArtifactPaths.OperationalContext;
    public const string DecisionsDirectory = OrchestrationArtifactPaths.DecisionsDirectory;
    public const string DeltasDirectory = OrchestrationArtifactPaths.DeltasDirectory;
    public const string HandoffsDirectory = OrchestrationArtifactPaths.HandoffsDirectory;
    public const string MilestonesDirectory = OrchestrationArtifactPaths.MilestonesDirectory;
    public const string MilestoneSearchPattern = OrchestrationArtifactPaths.MilestoneSearchPattern;
    public const string NonImplementationReviewDirectory = OrchestrationArtifactPaths.NonImplementationReviewDirectory;
    public const string CompletedEpicsDirectory = ".agents/archive/epics";
    public const string CompletedEpicsPattern = ".agents/archive/epics/*.md";
    public const string ExecutionEvidenceDirectory = ".agents/evidence/execution";
    public const string EvaluationEvidenceDirectory = ".agents/evidence/evaluations";
    public const string BlockerEvidenceDirectory = ".agents/evidence/blockers";

    public static string CompletedEpicArchiveDirectory(int index) => $"{CompletedEpicsDirectory}/{index}";

    public static string CompletedEpicSynthesis(int index) => $"{CompletedEpicsDirectory}/{index}.md";
}

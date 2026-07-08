using LoopRelay.Orchestration.Services;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public static class CompletionArtifactPaths
{
    public const string AgentsDirectory = OrchestrationArtifactPaths.AgentsDirectory;
    public const string EvidenceDirectory = OrchestrationArtifactPaths.EvidenceDirectory;
    public const string ActiveEpic = AgentsDirectory + "/epic.md";
    public const string CoreDirectory = AgentsDirectory + "/core";
    public const string RoadmapCompletionContext = CoreDirectory + "/roadmap-completion-context.md";
    public const string ExecutionPlan = OrchestrationArtifactPaths.Plan;
    public const string Details = OrchestrationArtifactPaths.Details;
    public const string OperationalContext = OrchestrationArtifactPaths.OperationalContext;
    public const string DecisionsDirectory = OrchestrationArtifactPaths.DecisionsDirectory;
    public const string DeltasDirectory = OrchestrationArtifactPaths.DeltasDirectory;
    public const string HandoffsDirectory = OrchestrationArtifactPaths.HandoffsDirectory;
    public const string MilestonesDirectory = OrchestrationArtifactPaths.MilestonesDirectory;
    public const string MilestoneSearchPattern = OrchestrationArtifactPaths.MilestoneSearchPattern;
    public const string NonImplementationReviewDirectory = OrchestrationArtifactPaths.NonImplementationReviewDirectory;
    public const string ArchiveDirectory = AgentsDirectory + "/archive";
    public const string CompletedEpicsDirectory = ArchiveDirectory + "/epics";
    public const string CompletedEpicsPattern = CompletedEpicsDirectory + "/*.md";
    public const string ExecutionEvidenceDirectory = EvidenceDirectory + "/execution";
    public const string EvaluationEvidenceDirectory = EvidenceDirectory + "/evaluations";
    public const string BlockerEvidenceDirectory = EvidenceDirectory + "/blockers";

    public static string CompletedEpicArchiveDirectory(int index) => $"{CompletedEpicsDirectory}/{index}";

    public static string CompletedEpicSynthesis(int index) => $"{CompletedEpicsDirectory}/{index}.md";
}

namespace LoopRelay.Completion;

public static class CompletionArtifactPaths
{
    public const string AgentsDirectory = ".agents";
    public const string ActiveEpic = ".agents/epic.md";
    public const string RoadmapCompletionContext = ".agents/core/roadmap-completion-context.md";
    public const string ExecutionPlan = ".agents/plan.md";
    public const string Details = ".agents/details.md";
    public const string OperationalContext = ".agents/operational_context.md";
    public const string DecisionsDirectory = ".agents/decisions";
    public const string DeltasDirectory = ".agents/deltas";
    public const string HandoffsDirectory = ".agents/handoffs";
    public const string MilestonesDirectory = ".agents/milestones";
    public const string MilestoneSearchPattern = "m*.md";
    public const string CompletedEpicsDirectory = ".agents/archive/epics";
    public const string CompletedEpicsPattern = ".agents/archive/epics/*.md";
    public const string ExecutionEvidenceDirectory = ".agents/evidence/execution";
    public const string EvaluationEvidenceDirectory = ".agents/evidence/evaluations";
    public const string BlockerEvidenceDirectory = ".agents/evidence/blockers";

    public static string CompletedEpicArchiveDirectory(int index) => $"{CompletedEpicsDirectory}/{index}";

    public static string CompletedEpicSynthesis(int index) => $"{CompletedEpicsDirectory}/{index}.md";
}

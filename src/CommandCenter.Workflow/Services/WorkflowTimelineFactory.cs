using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

internal static class WorkflowTimelineFactory
{
    public static WorkflowTimeline Create(
        WorkflowInstance projection,
        DateTimeOffset generatedAt,
        WorkflowStage? currentStage = null,
        WorkflowStage? previousStage = null)
    {
        WorkflowStage resolvedCurrentStage = currentStage ?? projection.CurrentStage;
        WorkflowStage resolvedPreviousStage = previousStage ??
            (projection.Timeline.Count == 0 ? WorkflowStage.Unknown : projection.Timeline[^1].Stage);
        string fingerprint = WorkflowFingerprint.ForTimeline(
            projection.RepositoryId,
            resolvedCurrentStage,
            resolvedPreviousStage,
            projection.ProgressState,
            projection.BlockingGate,
            projection.Timeline,
            projection.BlockedTransitions.Select(transition => transition.BlockingCondition).ToArray()).Value;

        return new WorkflowTimeline(
            projection.RepositoryId,
            resolvedCurrentStage,
            resolvedPreviousStage,
            projection.ProgressState,
            projection.BlockingGate,
            generatedAt,
            projection.Timeline,
            fingerprint);
    }
}

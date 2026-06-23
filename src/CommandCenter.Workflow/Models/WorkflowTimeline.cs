using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowTimeline(
    Guid RepositoryId,
    WorkflowStage CurrentStage,
    WorkflowStage PreviousStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<WorkflowTimelineEntry> Entries,
    string Fingerprint);

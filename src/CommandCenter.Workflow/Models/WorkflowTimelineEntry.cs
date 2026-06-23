using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowTimelineEntry(
    WorkflowTimelineEventType EventType,
    WorkflowStage Stage,
    DateTimeOffset OccurredAt,
    string Summary,
    string EvidenceId);

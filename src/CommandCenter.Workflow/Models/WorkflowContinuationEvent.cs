using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowContinuationEvent(
    Guid RepositoryId,
    string EventId,
    DateTimeOffset OccurredAt,
    string Trigger,
    WorkflowStage FromStage,
    WorkflowStage? ToStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    string Decision,
    string Reason,
    WorkflowContinuationFingerprint InputFingerprint,
    bool IsWaitingForHuman,
    bool IsComplete,
    string RequiredHumanAction,
    IReadOnlyList<string> Diagnostics);

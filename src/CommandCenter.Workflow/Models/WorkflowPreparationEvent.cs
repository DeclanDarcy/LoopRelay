using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowPreparationEvent(
    Guid RepositoryId,
    string EventId,
    DateTimeOffset OccurredAt,
    string Trigger,
    WorkflowStage Stage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    WorkflowPreparationCommand Command,
    string CommandName,
    string Decision,
    string Reason,
    WorkflowPreparationFingerprint InputFingerprint,
    bool IsWaitingForHuman,
    IReadOnlyList<string> CreatedArtifactIds,
    IReadOnlyList<string> Diagnostics);

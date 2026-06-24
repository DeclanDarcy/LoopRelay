using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowProgressionReport(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    int ValidTransitionCount,
    int BlockedTransitionCount,
    int ContinuationEventCount,
    IReadOnlyList<string> ValidTransitions,
    IReadOnlyList<string> BlockedTransitions,
    IReadOnlyList<string> ContinuationEvidence,
    IReadOnlyList<string> Diagnostics);

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowHistoryProjection(
    Guid RepositoryId,
    WorkflowTimeline Timeline,
    IReadOnlyList<string> GateHistory,
    IReadOnlyList<string> ProgressSummary,
    IReadOnlyList<WorkflowRecoveryDiagnostics> RecoverySummary);

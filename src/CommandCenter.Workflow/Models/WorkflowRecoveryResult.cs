namespace CommandCenter.Workflow.Models;

public sealed record WorkflowRecoveryResult(
    WorkflowTimeline Timeline,
    WorkflowRecoveryDiagnostics Diagnostics);

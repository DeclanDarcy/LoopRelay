namespace CommandCenter.Workflow.Primitives;

public enum WorkflowExecutionStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Cancelled,
    AwaitingAcceptance
}

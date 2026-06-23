namespace CommandCenter.Workflow.Primitives;

public enum WorkflowStage
{
    Unknown,
    WorkSelection,
    Execution,
    Handoff,
    Decision,
    OperationalContext,
    Commit,
    Push,
    Completed,
    Blocked,
    Failed
}

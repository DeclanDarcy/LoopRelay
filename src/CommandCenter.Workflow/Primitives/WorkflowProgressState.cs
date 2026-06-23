namespace CommandCenter.Workflow.Primitives;

public enum WorkflowProgressState
{
    Ready,
    Active,
    AwaitingGate,
    Blocked,
    Completed,
    Failed,
    Recovering,
    WaitingForHuman
}

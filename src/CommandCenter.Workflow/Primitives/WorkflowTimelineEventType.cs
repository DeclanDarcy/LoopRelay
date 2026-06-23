namespace CommandCenter.Workflow.Primitives;

public enum WorkflowTimelineEventType
{
    ExecutionStarted,
    ExecutionCompleted,
    DecisionResolved,
    OperationalContextPromoted,
    CommitExecuted,
    PushExecuted
}

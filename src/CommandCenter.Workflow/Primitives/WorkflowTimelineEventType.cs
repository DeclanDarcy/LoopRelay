namespace CommandCenter.Workflow.Primitives;

public enum WorkflowTimelineEventType
{
    ExecutionStarted,
    ExecutionCompleted,
    ExecutionHandoffAccepted,
    DecisionResolved,
    OperationalContextReviewed,
    OperationalContextPromoted,
    CommitExecuted,
    PushExecuted
}

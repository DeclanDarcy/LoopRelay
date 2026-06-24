namespace CommandCenter.Workflow.Primitives;

public enum WorkflowTimelineEventType
{
    ExecutionStarted,
    ExecutionCompleted,
    ExecutionFailed,
    ExecutionCancelled,
    ExecutionHandoffAccepted,
    ExecutionHandoffRejected,
    DecisionResolved,
    OperationalContextReviewed,
    OperationalContextPromoted,
    CommitExecuted,
    PushExecuted
}

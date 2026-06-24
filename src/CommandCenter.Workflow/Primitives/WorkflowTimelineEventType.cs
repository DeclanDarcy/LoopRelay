namespace CommandCenter.Workflow.Primitives;

public enum WorkflowTimelineEventType
{
    ExecutionStarted,
    ExecutionCompleted,
    ExecutionFailed,
    ExecutionCancelled,
    HandoffCreated,
    HandoffValidated,
    HandoffInvalid,
    ExecutionHandoffAccepted,
    ExecutionHandoffRejected,
    DecisionResolved,
    OperationalContextReviewed,
    OperationalContextPromoted,
    CommitExecuted,
    PushExecuted
}

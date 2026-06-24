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
    DecisionDiscovered,
    DecisionGenerated,
    DecisionReviewed,
    DecisionRefined,
    DecisionResolved,
    DecisionArchived,
    DecisionSuperseded,
    OperationalContextProposed,
    OperationalContextReviewed,
    OperationalContextAccepted,
    OperationalContextEdited,
    OperationalContextRejected,
    OperationalContextPromoted,
    OperationalContextArchived,
    CommitExecuted,
    PushExecuted
}

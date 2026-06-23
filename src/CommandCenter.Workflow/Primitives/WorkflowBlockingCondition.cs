namespace CommandCenter.Workflow.Primitives;

public enum WorkflowBlockingCondition
{
    MissingWorkSelection,
    MissingExecution,
    ExecutionRunning,
    ExecutionFailure,
    ExecutionCancelled,
    MissingHandoff,
    InvalidHandoff,
    PendingHandoffAcceptance,
    RejectedHandoff,
    MissingDecision,
    UnresolvedDecision,
    DecisionGovernanceBlock,
    PendingContextReview,
    PendingContextPromotion,
    PendingCommitApproval,
    PendingPushApproval,
    UnknownState,
    ConflictingEvidence,
    RecoveryConflict
}

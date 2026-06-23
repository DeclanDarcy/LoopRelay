namespace CommandCenter.Workflow.Primitives;

public enum WorkflowGateType
{
    None,
    WorkSelection,
    ExecutionAcceptance,
    DecisionResolution,
    OperationalContextReview,
    OperationalContextPromotion,
    CommitApproval,
    PushApproval
}

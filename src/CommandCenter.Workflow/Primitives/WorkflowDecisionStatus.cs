namespace CommandCenter.Workflow.Primitives;

public enum WorkflowDecisionStatus
{
    Missing,
    Discovered,
    Generated,
    UnderReview,
    AwaitingResolution,
    Resolved,
    Archived,
    Superseded
}

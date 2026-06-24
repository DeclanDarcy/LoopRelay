namespace CommandCenter.Workflow.Primitives;

public enum WorkflowOperationalContextStatus
{
    Missing,
    Proposed,
    UnderReview,
    Accepted,
    Edited,
    Rejected,
    ReadyForPromotion,
    Promoted,
    Archived,
    NoContextRequired
}

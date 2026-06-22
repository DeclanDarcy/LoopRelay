namespace CommandCenter.Decisions.Primitives;

public enum DecisionReviewState
{
    NotStarted,
    Viewed,
    NeedsRefinement,
    ReadyForResolution,
    Closed
}

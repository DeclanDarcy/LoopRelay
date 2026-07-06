namespace LoopRelay.Decisions.Primitives;

public enum DecisionProposalState
{
    Draft,
    Generated,
    Viewed,
    NeedsRefinement,
    ReadyForResolution,
    Refined,
    Resolved,
    Expired,
    Discarded
}

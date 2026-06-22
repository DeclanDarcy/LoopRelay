namespace CommandCenter.Decisions.Models;

public sealed record DecisionProposalRevisionSnapshot(
    DecisionProposalRevision Revision,
    DecisionProposalRevisionComparison Comparison,
    bool IsCurrentProposal,
    string AuthorityBoundary);

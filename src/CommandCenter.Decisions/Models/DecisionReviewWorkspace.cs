namespace CommandCenter.Decisions.Models;

public sealed record DecisionReviewWorkspace(
    DecisionProposal Proposal,
    DecisionReviewStatus Review,
    IReadOnlyList<DecisionReviewNote> Notes,
    IReadOnlyList<DecisionProposalRevision> Revisions,
    DecisionReviewDiagnostics Diagnostics,
    DecisionReviewAuthority Authority);

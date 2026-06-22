using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionProposalBrowserItem(
    string ProposalId,
    string CandidateId,
    DecisionProposalState State,
    string Title,
    DecisionClassification Classification,
    DecisionCandidatePriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DecisionReviewState ReviewState,
    DateTimeOffset ReviewUpdatedAt,
    bool IsResolved);

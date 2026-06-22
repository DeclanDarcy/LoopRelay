using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionProposal(
    string Id,
    Guid RepositoryId,
    string CandidateId,
    DecisionProposalState State,
    string Title,
    string Context,
    IReadOnlyList<DecisionOption> Options,
    IReadOnlyList<DecisionTradeoff> Tradeoffs,
    DecisionRecommendation? Recommendation,
    IReadOnlyList<DecisionAssumption> Assumptions,
    IReadOnlyList<DecisionEvidence> Evidence,
    IReadOnlyList<DecisionHistoryEntry> History);

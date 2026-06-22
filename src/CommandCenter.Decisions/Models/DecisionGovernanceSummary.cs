namespace CommandCenter.Decisions.Models;

public sealed record DecisionGovernanceSummary(
    int DecisionCount,
    int ResolvedDecisionCount,
    int ActiveCandidateCount,
    int ActiveProposalCount,
    int AssimilationRecommendationCount,
    int FindingCount,
    int BlockingFindingCount);

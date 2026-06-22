namespace CommandCenter.Decisions.Models;

public sealed record DecisionOptionComparison(
    string ProposalId,
    string? RecommendedOptionId,
    IReadOnlyList<DecisionOptionComparisonItem> Options);

namespace CommandCenter.Decisions.Models;

public sealed record DecisionRefinementRequest(
    string Reason,
    string? Context = null,
    IReadOnlyList<DecisionOption>? Options = null,
    IReadOnlyList<DecisionTradeoff>? Tradeoffs = null,
    DecisionRecommendation? Recommendation = null,
    IReadOnlyList<DecisionAssumption>? Assumptions = null);

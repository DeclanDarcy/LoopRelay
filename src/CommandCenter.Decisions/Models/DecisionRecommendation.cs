namespace CommandCenter.Decisions.Models;

public sealed record DecisionRecommendation(
    string OptionId,
    string Rationale,
    IReadOnlyList<DecisionEvidence> Evidence);

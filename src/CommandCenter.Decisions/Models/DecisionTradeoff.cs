namespace CommandCenter.Decisions.Models;

public sealed record DecisionTradeoff(
    string OptionId,
    string Benefit,
    string Cost,
    IReadOnlyList<DecisionEvidence> Evidence);

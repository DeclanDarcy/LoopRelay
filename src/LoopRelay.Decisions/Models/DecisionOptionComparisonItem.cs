namespace LoopRelay.Decisions.Models;

public sealed record DecisionOptionComparisonItem(
    string OptionId,
    string Title,
    string Description,
    bool IsRecommended,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Costs,
    IReadOnlyList<DecisionEvidence> Evidence);

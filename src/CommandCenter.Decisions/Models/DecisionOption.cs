namespace CommandCenter.Decisions.Models;

public sealed record DecisionOption(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<DecisionEvidence> Evidence);

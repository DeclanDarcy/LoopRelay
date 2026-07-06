namespace LoopRelay.Decisions.Models;

public sealed record DecisionAssumption(
    string Id,
    string Statement,
    IReadOnlyList<DecisionEvidence> Evidence);

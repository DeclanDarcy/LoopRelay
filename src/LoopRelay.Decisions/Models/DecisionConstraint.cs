namespace LoopRelay.Decisions.Models;

public sealed record DecisionConstraint(
    string Id,
    string Statement,
    IReadOnlyList<DecisionEvidence> Evidence);

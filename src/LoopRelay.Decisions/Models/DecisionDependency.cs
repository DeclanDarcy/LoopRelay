namespace LoopRelay.Decisions.Models;

public sealed record DecisionDependency(
    string Statement,
    IReadOnlyList<DecisionEvidence> Evidence);

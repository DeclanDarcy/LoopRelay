namespace LoopRelay.Decisions.Models;

public sealed record DecisionEvidence(
    string Summary,
    IReadOnlyList<DecisionSourceReference> Sources);

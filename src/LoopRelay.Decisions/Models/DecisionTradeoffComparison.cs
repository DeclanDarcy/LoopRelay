namespace LoopRelay.Decisions.Models;

public sealed record DecisionTradeoffComparison(
    string OptionId,
    IReadOnlyList<string> RelativeStrengths,
    IReadOnlyList<string> RelativeWeaknesses,
    IReadOnlyList<string> UniqueAdvantages,
    IReadOnlyList<string> UniqueRisks,
    IReadOnlyList<string> DisqualifyingConstraints,
    IReadOnlyList<DecisionEvidence> Evidence);

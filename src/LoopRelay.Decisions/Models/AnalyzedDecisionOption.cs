namespace LoopRelay.Decisions.Models;

public sealed record AnalyzedDecisionOption(
    string OptionId,
    IReadOnlyList<DecisionBenefit> Benefits,
    IReadOnlyList<DecisionCost> Costs,
    IReadOnlyList<DecisionRisk> Risks,
    IReadOnlyList<DecisionDependency> Dependencies,
    IReadOnlyList<DecisionConsequence> Consequences,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<DecisionEvidence> Evidence);

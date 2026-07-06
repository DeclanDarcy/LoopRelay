namespace LoopRelay.Continuity.Models;

public sealed class DecisionAnalysisResult
{
    public IReadOnlyList<DecisionSignal> Signals { get; init; } = [];

    public IReadOnlyList<ContinuityDecisionConsequence> Consequences { get; init; } = [];

    public IReadOnlyList<ContinuityDecisionContradiction> Contradictions { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

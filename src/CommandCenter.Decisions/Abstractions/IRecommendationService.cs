using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IRecommendationService
{
    DecisionRecommendation GenerateRecommendation(
        DecisionCandidate candidate,
        DecisionGenerationContext generationContext,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<AnalyzedDecisionOption> analyzedOptions,
        IReadOnlyList<DecisionTradeoffComparison> tradeoffComparisons,
        IReadOnlyList<DecisionEvidence> evidence);
}

using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

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

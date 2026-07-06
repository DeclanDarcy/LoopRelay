using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface ITradeoffAnalysisService
{
    IReadOnlyList<AnalyzedDecisionOption> AnalyzeOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence,
        DecisionGenerationContext generationContext,
        string contextFingerprint);
}

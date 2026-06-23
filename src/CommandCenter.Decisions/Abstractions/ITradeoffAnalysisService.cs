using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface ITradeoffAnalysisService
{
    IReadOnlyList<AnalyzedDecisionOption> AnalyzeOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence,
        string contextFingerprint);
}

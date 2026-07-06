using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IOptionComparisonService
{
    IReadOnlyList<DecisionTradeoffComparison> CompareOptions(
        DecisionCandidate candidate,
        IReadOnlyList<AnalyzedDecisionOption> analyzedOptions,
        IReadOnlyList<DecisionOptionRelationship> relationships,
        IReadOnlyList<DecisionEvidence> evidence);
}

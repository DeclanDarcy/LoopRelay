using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionResolvedProposalSnapshot(
    string ProposalId,
    string CandidateId,
    string ProposalFingerprint,
    DecisionProposalState ProposalState,
    string Title,
    string Context,
    IReadOnlyList<DecisionOption> Options,
    IReadOnlyList<DecisionTradeoff> Tradeoffs,
    DecisionRecommendation? Recommendation,
    IReadOnlyList<DecisionAssumption> Assumptions,
    IReadOnlyList<DecisionEvidence> Evidence,
    IReadOnlyList<DecisionHistoryEntry> History,
    IReadOnlyList<DecisionProposalRevision> Revisions)
{
    public IReadOnlyList<DecisionOptionRelationship> OptionRelationships { get; init; } = [];

    public IReadOnlyList<AnalyzedDecisionOption> AnalyzedOptions { get; init; } = [];

    public IReadOnlyList<DecisionTradeoffComparison> TradeoffComparisons { get; init; } = [];

    public DecisionTradeoffAnalysisDiagnostics? TradeoffAnalysisDiagnostics { get; init; }

    public DecisionGenerationDiagnostics? GenerationDiagnostics { get; init; }
}

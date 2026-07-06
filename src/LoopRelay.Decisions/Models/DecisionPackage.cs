namespace LoopRelay.Decisions.Models;

public sealed record DecisionPackage(
    string Id,
    Guid RepositoryId,
    string ProposalId,
    string CandidateId,
    string Title,
    string DecisionSummary,
    DecisionCandidate Candidate,
    DecisionGenerationContext ContextSummary,
    IReadOnlyList<DecisionOption> Options,
    IReadOnlyList<DecisionOptionRelationship> OptionRelationships,
    IReadOnlyList<AnalyzedDecisionOption> AnalyzedOptions,
    IReadOnlyList<DecisionTradeoff> Tradeoffs,
    IReadOnlyList<DecisionTradeoffComparison> TradeoffComparisons,
    DecisionRecommendation? Recommendation,
    IReadOnlyList<DecisionAssumption> Assumptions,
    IReadOnlyList<string> OpenConcerns,
    IReadOnlyList<DecisionEvidence> Evidence,
    DecisionPackageMetadata Metadata,
    DecisionGenerationDiagnostics? GenerationDiagnostics,
    DecisionTradeoffAnalysisDiagnostics? TradeoffAnalysisDiagnostics,
    DateTimeOffset GeneratedAt);

namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationRepositoryReport(
    int CandidateCount,
    int AutomaticallyDiscoveredCandidateCount,
    int GeneratedProposalCount,
    int GeneratedPackageCount,
    int GeneratedResolvedDecisionCount,
    int QualityAssessmentCount,
    int ExecutionInfluenceTraceCount,
    int ManualBypassCount,
    IReadOnlyList<string> Diagnostics);

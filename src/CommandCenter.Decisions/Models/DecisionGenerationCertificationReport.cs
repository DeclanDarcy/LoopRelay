namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationCertificationReport(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string InputFingerprint,
    DecisionGenerationCertificationResult Result,
    int CandidateCount,
    int GeneratedProposalCount,
    int GeneratedPackageCount,
    int GeneratedResolvedDecisionCount,
    int ExecutionInfluenceTraceCount,
    HumanAuthoringBurdenReport HumanAuthoringBurden,
    IReadOnlyList<DecisionQualityAssessment> QualityAssessments,
    DecisionGenerationRepositoryReport RepositoryReport,
    DecisionGenerationWorkflowReport WorkflowReport,
    DecisionGenerationHumanAuthoringBurdenReport HumanAuthoringBurdenSummary,
    DecisionGenerationExecutiveReport ExecutiveReport,
    IReadOnlyList<string> Diagnostics);

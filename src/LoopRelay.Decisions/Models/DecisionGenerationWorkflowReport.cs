namespace LoopRelay.Decisions.Models;

public sealed record DecisionGenerationWorkflowReport(
    int GeneratedResolvedDecisionCount,
    int HumanResolvedGeneratedDecisionCount,
    int SystemResolvedGeneratedDecisionCount,
    int PreservedHistoryDecisionCount,
    int RecommendationDivergenceCount,
    double RecommendationDivergenceRate,
    int ExecutionInfluenceCoveredDecisionCount,
    double ExecutionInfluenceCoverageRate,
    IReadOnlyList<string> Diagnostics);

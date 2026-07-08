namespace LoopRelay.Completion.Models;

public sealed record CompletionEvaluationDecision(
    string OverallCompletionStatus,
    string OverallDriftClassification,
    string ClosureRecommendation);

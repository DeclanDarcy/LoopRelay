namespace LoopRelay.Completion;

public sealed record CompletionEvaluationDecision(
    string OverallCompletionStatus,
    string OverallDriftClassification,
    string ClosureRecommendation);

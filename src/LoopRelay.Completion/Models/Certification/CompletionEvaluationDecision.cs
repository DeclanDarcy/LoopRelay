namespace LoopRelay.Completion.Models.Certification;

public sealed record CompletionEvaluationDecision(
    string OverallCompletionStatus,
    string OverallDriftClassification,
    string ClosureRecommendation);

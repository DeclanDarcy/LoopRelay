namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationPostExecutionReviewResult(
    string ExecutionSliceId,
    IReadOnlyList<string> EvidencePaths,
    NonImplementationPostExecutionReviewSummary Summary);

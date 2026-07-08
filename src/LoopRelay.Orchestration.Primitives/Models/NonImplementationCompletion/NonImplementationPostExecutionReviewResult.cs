namespace LoopRelay.Orchestration.Models.NonImplementationCompletion;

public sealed record NonImplementationPostExecutionReviewResult(
    string ExecutionSliceId,
    IReadOnlyList<string> EvidencePaths,
    NonImplementationPostExecutionReviewSummary Summary);

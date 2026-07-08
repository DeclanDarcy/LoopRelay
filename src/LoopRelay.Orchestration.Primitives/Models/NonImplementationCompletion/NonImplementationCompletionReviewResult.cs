using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationCompletion;

public sealed record NonImplementationCompletionReviewResult(
    NonImplementationCompletionReviewStatus Status,
    string ReviewPath,
    string? DecisionTemplatePath,
    IReadOnlyList<string> EvidencePaths,
    IReadOnlyList<string> AppliedDeletePaths,
    NonImplementationCompletionReviewSummary Summary,
    IReadOnlyList<string> BlockerMessages)
{
    public bool IsReady => Status == NonImplementationCompletionReviewStatus.Ready;

    public bool IsBlocked => Status == NonImplementationCompletionReviewStatus.Blocked;
}

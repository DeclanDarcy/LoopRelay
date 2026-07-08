namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public enum NonImplementationCompletionReviewStatus
{
    Ready,
    Blocked,
}

public sealed record NonImplementationCompletionReviewSummary(
    string RefreshId,
    int CurrentChangedFileCount,
    int ClassifiedFileCount,
    int SemanticCandidateCount,
    int ConfirmedThisRefreshCount,
    int ReusedSemanticDispositionCount,
    int UnresolvedConfirmedNonImplementationCount,
    int UnresolvedSemanticUncertaintyCount,
    int ResolvedFileDecisionCount,
    int AppliedDeleteCount,
    bool SynthesisDecisionRequired,
    NonImplementationSynthesisDecision? SynthesisDecision);

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

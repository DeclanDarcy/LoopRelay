namespace LoopRelay.Orchestration.Models.NonImplementationReview;
public static class NonImplementationReviewOwnership
{
    public const string SliceBaselineAndChangeDetection = "LoopRelay.Orchestration.Primitives";
    public const string SemanticConfirmationAndSynthesis = "INonImplementationReviewRunner";
    public const string MainCliPostExecutionInvocation =
        "LoopRelay.Cli after execution writes and before .agents post-execution publish";
    public const string EpicCompletionReview =
        "LoopRelay.Completion before final completion evaluation closes the epic";
    public const string RoadmapPlanningPromptPolicy =
        "Centralized ImplementationFirstPromptPolicyComposer";
}

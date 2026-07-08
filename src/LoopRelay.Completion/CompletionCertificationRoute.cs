namespace LoopRelay.Completion;

public sealed record CompletionCertificationRoute(
    string ClosureRecommendation,
    CompletionTransitionIntent Intent,
    bool RequiresRoadmapCompletionContextUpdate,
    bool ShouldCloseEpic);

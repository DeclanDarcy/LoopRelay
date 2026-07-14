using LoopRelay.Completion.Primitives;

namespace LoopRelay.Completion.Models.Certification;

public sealed record CompletionCertificationRoute(
    string ClosureRecommendation,
    CompletionTransitionIntent Intent,
    bool RequiresRoadmapCompletionContextUpdate,
    bool ShouldCloseEpic);

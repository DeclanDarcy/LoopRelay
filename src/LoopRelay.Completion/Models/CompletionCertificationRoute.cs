using LoopRelay.Completion.Primitives;

namespace LoopRelay.Completion.Models;

public sealed record CompletionCertificationRoute(
    string ClosureRecommendation,
    CompletionTransitionIntent Intent,
    bool RequiresRoadmapCompletionContextUpdate,
    bool ShouldCloseEpic);

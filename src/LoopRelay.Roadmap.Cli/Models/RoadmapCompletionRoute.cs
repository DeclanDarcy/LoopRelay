using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapCompletionRoute(
    CompletionCertificationRoute Shared,
    RoadmapState TargetState,
    TransitionStatus TransitionStatus,
    RoadmapOutcome CliOutcome,
    ArtifactLifecycleState? ActiveEpicLifecycleState,
    IReadOnlyList<string> NextTransitions)
{
    public string ClosureRecommendation => Shared.ClosureRecommendation;

    public CompletionTransitionIntent Intent => Shared.Intent;

    public bool RequiresRoadmapCompletionContextUpdate => Shared.RequiresRoadmapCompletionContextUpdate;

    public RoadmapTransitionIntent ToRoadmapTransitionIntent(string evidencePath) =>
        new(Intent.ToString(), TargetState, [evidencePath]);
}

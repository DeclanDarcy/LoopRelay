using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Primitives;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapTracking;

internal sealed record RoadmapCompletionRoute(
    CompletionCertificationRoute Shared,
    Primitives.State.RoadmapState TargetState,
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

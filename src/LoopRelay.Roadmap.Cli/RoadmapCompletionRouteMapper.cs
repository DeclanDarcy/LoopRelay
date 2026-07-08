using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal static class RoadmapCompletionRouteMapper
{
    public static RoadmapCompletionRoute Map(CompletionCertificationRoute route) =>
        route.ClosureRecommendation switch
        {
            "Close Epic" => Close(route),
            "Close With Follow-Up" => Close(route),
            "Continue Epic" => new(
                route,
                RoadmapState.ExecutionLoop,
                TransitionStatus.Paused,
                RoadmapOutcome.Paused,
                ArtifactLifecycleState.Executing,
                ["ContinueExecution"]),
            "Reopen Epic" => new(
                route,
                RoadmapState.EpicPreparationAudit,
                TransitionStatus.Paused,
                RoadmapOutcome.Paused,
                ArtifactLifecycleState.Ready,
                ["EpicPreparationAudit"]),
            "Gather More Evidence" => new(
                route,
                RoadmapState.EvidenceGathering,
                TransitionStatus.Paused,
                RoadmapOutcome.Paused,
                ArtifactLifecycleState.Ready,
                ["GatherAdditionalEvidence", "EvaluateEpicCompletionAndDrift"]),
            _ => throw new InvalidOperationException(
                $"No Roadmap completion route registered for `{route.ClosureRecommendation}`."),
        };

    private static RoadmapCompletionRoute Close(CompletionCertificationRoute route) =>
        new(
            route,
            RoadmapState.SelectNextStrategicInitiative,
            TransitionStatus.Completed,
            RoadmapOutcome.Completed,
            ArtifactLifecycleState.Completed,
            ["SelectNextEpic"]);
}

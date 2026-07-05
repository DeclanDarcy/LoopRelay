namespace CommandCenter.Roadmap.Cli;

internal sealed class CompletionCertificationRouter
{
    private static readonly CompletionCertificationRoute[] DefaultRoutes =
    [
        new(
            "Close Epic",
            CompletionTransitionIntent.UpdateRoadmapCompletionContext,
            RoadmapState.SelectNextStrategicInitiative,
            TransitionStatus.Completed,
            RoadmapOutcome.Completed,
            RequiresRoadmapCompletionContextUpdate: true,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Completed,
            NextTransitions: ["SelectNextEpic"]),
        new(
            "Close With Follow-Up",
            CompletionTransitionIntent.UpdateRoadmapCompletionContext,
            RoadmapState.SelectNextStrategicInitiative,
            TransitionStatus.Completed,
            RoadmapOutcome.Completed,
            RequiresRoadmapCompletionContextUpdate: true,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Completed,
            NextTransitions: ["SelectNextEpic"]),
        new(
            "Continue Epic",
            CompletionTransitionIntent.ContinueExecution,
            RoadmapState.ExecutionLoop,
            TransitionStatus.Paused,
            RoadmapOutcome.Paused,
            RequiresRoadmapCompletionContextUpdate: false,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Executing,
            NextTransitions: ["ContinueExecution"]),
        new(
            "Reopen Epic",
            CompletionTransitionIntent.ReturnToEpicPreparationAudit,
            RoadmapState.EpicPreparationAudit,
            TransitionStatus.Paused,
            RoadmapOutcome.Paused,
            RequiresRoadmapCompletionContextUpdate: false,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Ready,
            NextTransitions: ["EpicPreparationAudit"]),
        new(
            "Gather More Evidence",
            CompletionTransitionIntent.GatherAdditionalEvidence,
            RoadmapState.EvidenceGathering,
            TransitionStatus.Paused,
            RoadmapOutcome.Paused,
            RequiresRoadmapCompletionContextUpdate: false,
            ActiveEpicLifecycleState: ArtifactLifecycleState.Ready,
            NextTransitions: ["GatherAdditionalEvidence", "EvaluateEpicCompletionAndDrift"]),
    ];

    private readonly IReadOnlyDictionary<string, CompletionCertificationRoute> routes;

    public CompletionCertificationRouter()
        : this(DefaultRoutes)
    {
    }

    internal CompletionCertificationRouter(IEnumerable<CompletionCertificationRoute> routes)
    {
        this.routes = routes.ToDictionary(route => route.ClosureRecommendation, StringComparer.Ordinal);
        EnsureComplete(this.routes.Keys);
    }

    public static IReadOnlyList<string> AllowedRecommendations =>
        CompletionCertificationVocabulary.ClosureRecommendations;

    public IReadOnlyCollection<CompletionCertificationRoute> All => this.routes.Values.ToList();

    public CompletionCertificationRoute Route(CompletionEvaluationDecision decision) =>
        this.routes.TryGetValue(decision.ClosureRecommendation, out CompletionCertificationRoute? route)
            ? route
            : throw new InvalidOperationException($"No completion certification route registered for `{decision.ClosureRecommendation}`.");

    private static void EnsureComplete(IEnumerable<string> routeKeys)
    {
        HashSet<string> registered = routeKeys.ToHashSet(StringComparer.Ordinal);
        string[] missing = AllowedRecommendations
            .Where(recommendation => !registered.Contains(recommendation))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException("Completion certification route table is missing routes for: " + string.Join(", ", missing));
        }

        string[] unexpected = registered
            .Where(recommendation => !AllowedRecommendations.Contains(recommendation, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException("Completion certification route table contains unsupported routes: " + string.Join(", ", unexpected));
        }
    }
}

internal sealed record CompletionCertificationRoute(
    string ClosureRecommendation,
    CompletionTransitionIntent Intent,
    RoadmapState TargetState,
    TransitionStatus TransitionStatus,
    RoadmapOutcome CliOutcome,
    bool RequiresRoadmapCompletionContextUpdate,
    ArtifactLifecycleState? ActiveEpicLifecycleState,
    IReadOnlyList<string> NextTransitions)
{
    public RoadmapTransitionIntent ToRoadmapTransitionIntent(string evidencePath) =>
        new(Intent.ToString(), TargetState, [evidencePath]);
}

internal enum CompletionTransitionIntent
{
    UpdateRoadmapCompletionContext,
    ContinueExecution,
    ReturnToEpicPreparationAudit,
    GatherAdditionalEvidence,
}

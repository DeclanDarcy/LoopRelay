using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Primitives;

namespace LoopRelay.Completion.Services.Certification;

public sealed class CompletionCertificationRouter
{
    private static readonly CompletionCertificationRoute[] DefaultRoutes =
    [
        new(
            "Close Epic",
            CompletionTransitionIntent.UpdateRoadmapCompletionContext,
            RequiresRoadmapCompletionContextUpdate: true,
            ShouldCloseEpic: true),
        new(
            "Close With Follow-Up",
            CompletionTransitionIntent.UpdateRoadmapCompletionContext,
            RequiresRoadmapCompletionContextUpdate: true,
            ShouldCloseEpic: true),
        new(
            "Continue Epic",
            CompletionTransitionIntent.ContinueExecution,
            RequiresRoadmapCompletionContextUpdate: false,
            ShouldCloseEpic: false),
        new(
            "Reopen Epic",
            CompletionTransitionIntent.ReturnToEpicPreparationAudit,
            RequiresRoadmapCompletionContextUpdate: false,
            ShouldCloseEpic: false),
        new(
            "Gather More Evidence",
            CompletionTransitionIntent.GatherAdditionalEvidence,
            RequiresRoadmapCompletionContextUpdate: false,
            ShouldCloseEpic: false),
    ];

    private readonly IReadOnlyDictionary<string, CompletionCertificationRoute> _routes;

    public CompletionCertificationRouter()
        : this(DefaultRoutes)
    {
    }

    public CompletionCertificationRouter(IEnumerable<CompletionCertificationRoute> routes)
    {
        _routes = routes.ToDictionary(route => route.ClosureRecommendation, StringComparer.Ordinal);
        EnsureComplete(_routes.Keys);
    }

    public static IReadOnlyList<string> AllowedRecommendations =>
        CompletionCertificationVocabulary.ClosureRecommendations;

    public IReadOnlyCollection<CompletionCertificationRoute> All => _routes.Values.ToList();

    public CompletionCertificationRoute Route(CompletionEvaluationDecision decision) =>
        _routes.TryGetValue(decision.ClosureRecommendation, out CompletionCertificationRoute? route)
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

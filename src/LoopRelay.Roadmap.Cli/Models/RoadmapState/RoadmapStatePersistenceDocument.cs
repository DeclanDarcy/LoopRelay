using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record RoadmapStatePersistenceDocument(
    string SchemaVersion,
    Primitives.State.RoadmapState CurrentState,
    IReadOnlyList<RoadmapArtifactStateDto> ActiveArtifacts,
    RoadmapTransitionSummaryDto LastTransition,
    IReadOnlyList<RoadmapBlockerDto> Blockers,
    string LastDecisionId,
    int RetiredEpicsCount,
    int SplitFamiliesCount,
    ProjectionManifestCountsDto ProjectionManifestCounts,
    RoadmapTransitionIntentDto TransitionIntent,
    IReadOnlyList<string> NextValidTransitions,
    IReadOnlyList<RetiredEpicDto> RetiredEpics)
{
    public const string CurrentSchemaVersion = "roadmap-state.v1";

    public static RoadmapStatePersistenceDocument FromDomain(RoadmapStateDocument document) =>
        new(
            CurrentSchemaVersion,
            document.CurrentState,
            document.ActiveArtifacts.Select(RoadmapArtifactStateDto.FromDomain).ToArray(),
            RoadmapTransitionSummaryDto.FromDomain(document.LastTransition),
            document.Blockers.Select(RoadmapBlockerDto.FromDomain).ToArray(),
            document.LastDecisionId,
            document.RetiredEpicsCount,
            document.SplitFamiliesCount,
            ProjectionManifestCountsDto.FromDomain(document.ProjectionManifestCounts),
            RoadmapTransitionIntentDto.FromDomain(document.TransitionIntent),
            document.NextValidTransitions.ToArray(),
            document.RetiredEpics.Select(RetiredEpicDto.FromDomain).ToArray());

    public RoadmapStateDocument ToDomain() =>
        new(
            CurrentState,
            ActiveArtifacts.Select(row => row.ToDomain()).ToArray(),
            LastTransition.ToDomain(),
            Blockers.Select(blocker => blocker.ToDomain()).ToArray(),
            LastDecisionId,
            RetiredEpicsCount,
            SplitFamiliesCount,
            ProjectionManifestCounts.ToDomain(),
            TransitionIntent.ToDomain(),
            NextValidTransitions.ToArray(),
            RetiredEpics.Select(retired => retired.ToDomain()).ToArray());

    public static IReadOnlyList<string> Validate(RoadmapStatePersistenceDocument document)
    {
        var errors = new List<string>();
        if (document.ActiveArtifacts.Any(row => string.IsNullOrWhiteSpace(row.Path)))
        {
            errors.Add("Active artifact rows must include a path.");
        }

        if (document.Blockers.Any(row => string.IsNullOrWhiteSpace(row.Blocker)))
        {
            errors.Add("Blocker rows must include a blocker.");
        }

        if (document.LastTransition is null)
        {
            errors.Add("Last transition is required.");
        }

        if (document.ProjectionManifestCounts is null)
        {
            errors.Add("Projection manifest counts are required.");
        }

        if (document.TransitionIntent is null)
        {
            errors.Add("Transition intent is required.");
        }

        if (document.RetiredEpicsCount < 0)
        {
            errors.Add("Retired epic count cannot be negative.");
        }

        if (document.SplitFamiliesCount < 0)
        {
            errors.Add("Split family count cannot be negative.");
        }

        if (document.ProjectionManifestCounts is { Valid: < 0 } or { Stale: < 0 } or { Invalid: < 0 })
        {
            errors.Add("Projection manifest counts cannot be negative.");
        }

        foreach (RetiredEpic retired in document.RetiredEpics.Select(retired => retired.ToDomain()))
        {
            if (!retired.HasStableIdentity)
            {
                errors.Add("Retired epic records must include a stable identity.");
            }
        }

        return errors;
    }
}

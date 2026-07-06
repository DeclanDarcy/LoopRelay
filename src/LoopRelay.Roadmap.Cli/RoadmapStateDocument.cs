namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapStateDocument(
    RoadmapState CurrentState,
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    RoadmapTransitionSummary LastTransition,
    IReadOnlyList<BlockerRow> Blockers,
    string LastDecisionId,
    int RetiredEpicsCount,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts,
    RoadmapTransitionIntent TransitionIntent,
    IReadOnlyList<string> NextValidTransitions,
    IReadOnlyList<RetiredEpic> RetiredEpics);

internal sealed record ArtifactStateRow(string Artifact, string Path, string Status);

internal sealed record BlockerRow(string Blocker, string RequiredNextStep);

internal sealed record ProjectionManifestCounts(int Valid, int Stale, int Invalid);

internal sealed record RoadmapTransitionSummary(
    RoadmapState From,
    RoadmapState To,
    string Prompt,
    string Projection,
    string Output,
    string Decision,
    TransitionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

internal sealed record RoadmapTransitionIntent(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntent Empty(RoadmapState dispatchState) => new("None", dispatchState, []);
}

internal enum TransitionStatus
{
    Started,
    PromptCompleted,
    Completed,
    Paused,
    Failed,
    Cancelled,
}

internal sealed record RoadmapStatePersistenceDocument(
    string SchemaVersion,
    RoadmapState CurrentState,
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

internal sealed record RoadmapArtifactStateDto(string Artifact, string Path, string Status)
{
    public static RoadmapArtifactStateDto FromDomain(ArtifactStateRow row) => new(row.Artifact, row.Path, row.Status);

    public ArtifactStateRow ToDomain() => new(Artifact, Path, Status);
}

internal sealed record RoadmapBlockerDto(string Blocker, string RequiredNextStep)
{
    public static RoadmapBlockerDto FromDomain(BlockerRow row) => new(row.Blocker, row.RequiredNextStep);

    public BlockerRow ToDomain() => new(Blocker, RequiredNextStep);
}

internal sealed record ProjectionManifestCountsDto(int Valid, int Stale, int Invalid)
{
    public static ProjectionManifestCountsDto FromDomain(ProjectionManifestCounts counts) => new(counts.Valid, counts.Stale, counts.Invalid);

    public ProjectionManifestCounts ToDomain() => new(Valid, Stale, Invalid);
}

internal sealed record RoadmapTransitionSummaryDto(
    RoadmapState From,
    RoadmapState To,
    string Prompt,
    string Projection,
    string Output,
    string Decision,
    TransitionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static RoadmapTransitionSummaryDto FromDomain(RoadmapTransitionSummary transition) =>
        new(
            transition.From,
            transition.To,
            transition.Prompt,
            transition.Projection,
            transition.Output,
            transition.Decision,
            transition.Status,
            transition.StartedAt,
            transition.CompletedAt);

    public RoadmapTransitionSummary ToDomain() =>
        new(From, To, Prompt, Projection, Output, Decision, Status, StartedAt, CompletedAt);
}

internal sealed record RoadmapTransitionIntentDto(
    string Intent,
    RoadmapState DispatchState,
    IReadOnlyList<string> EvidencePaths)
{
    public static RoadmapTransitionIntentDto FromDomain(RoadmapTransitionIntent intent) =>
        new(intent.Intent, intent.DispatchState, intent.EvidencePaths.ToArray());

    public RoadmapTransitionIntent ToDomain() => new(Intent, DispatchState, EvidencePaths.ToArray());
}

internal sealed record RetiredEpicDto(
    string EpicId,
    string EpicName,
    string PrimaryReason,
    string AuditEvidencePath,
    DateTimeOffset RetiredAt)
{
    public static RetiredEpicDto FromDomain(RetiredEpic retired) =>
        new(retired.EpicId, retired.EpicName, retired.PrimaryReason, retired.AuditEvidencePath, retired.RetiredAt);

    public RetiredEpic ToDomain() => new(EpicId, EpicName, PrimaryReason, AuditEvidencePath, RetiredAt);
}

namespace CommandCenter.Roadmap.Cli;

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

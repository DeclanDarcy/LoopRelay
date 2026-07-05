namespace CommandCenter.Roadmap.Cli;

internal sealed record RoadmapStateDocument(
    RoadmapState CurrentState,
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    RoadmapTransitionSummary LastTransition,
    IReadOnlyList<BlockerRow> Blockers,
    string LastDecisionId,
    int RetiredExclusionsCount,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts,
    IReadOnlyList<string> NextValidTransitions,
    IReadOnlyList<string> RetiredEpicExclusions);

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

internal enum TransitionStatus
{
    Started,
    Completed,
    Failed,
    Cancelled,
}

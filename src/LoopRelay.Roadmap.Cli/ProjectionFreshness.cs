namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectionFreshness(
    ProjectionStaleStatus Status,
    IReadOnlyList<ProjectionStaleReason> Reasons)
{
    public bool IsFresh => Status == ProjectionStaleStatus.Fresh;

    public static ProjectionFreshness Fresh { get; } = new(ProjectionStaleStatus.Fresh, []);

    public static ProjectionFreshness Stale(params ProjectionStaleReason[] reasons) =>
        new(ProjectionStaleStatus.Stale, NormalizeReasons(reasons));

    public static ProjectionFreshness Unknown(params ProjectionStaleReason[] reasons) =>
        new(ProjectionStaleStatus.UnknownProvenance, NormalizeReasons(reasons));

    private static IReadOnlyList<ProjectionStaleReason> NormalizeReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0
            ? [ProjectionStaleReason.UnknownProvenance]
            : reasons.Distinct().OrderBy(reason => reason.ToString(), StringComparer.Ordinal).ToArray();
}

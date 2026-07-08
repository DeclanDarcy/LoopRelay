using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;

internal sealed record DerivedArtifactFreshness(
    DerivedArtifactFreshnessStatus Status,
    IReadOnlyList<DerivedArtifactStaleReason> Reasons)
{
    public bool IsFresh => Status == DerivedArtifactFreshnessStatus.Fresh;

    public static DerivedArtifactFreshness Fresh { get; } = new(DerivedArtifactFreshnessStatus.Fresh, []);

    public static DerivedArtifactFreshness Stale(params DerivedArtifactStaleReason[] reasons) =>
        new(DerivedArtifactFreshnessStatus.Stale, NormalizeReasons(reasons));

    public static DerivedArtifactFreshness Unknown(params DerivedArtifactStaleReason[] reasons) =>
        new(DerivedArtifactFreshnessStatus.UnknownProvenance, NormalizeReasons(reasons));

    public static DerivedArtifactFreshness Combine(params DerivedArtifactFreshness[] results)
    {
        if (results.All(result => result.IsFresh))
        {
            return Fresh;
        }

        DerivedArtifactStaleReason[] reasons = results
            .Where(result => !result.IsFresh)
            .SelectMany(result => result.Reasons)
            .ToArray();

        return results.Any(result => result.Status == DerivedArtifactFreshnessStatus.UnknownProvenance)
            ? Unknown(reasons)
            : Stale(reasons);
    }

    private static IReadOnlyList<DerivedArtifactStaleReason> NormalizeReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0
            ? [DerivedArtifactStaleReason.UnknownProvenance]
            : reasons.Distinct().OrderBy(reason => reason.ToString(), StringComparer.Ordinal).ToArray();
}

namespace LoopRelay.Roadmap.Cli.Models.ArtifactBundles;

internal sealed record BundleExtractionResult(bool IsBlocked, IReadOnlyList<ExtractedBundleFile> Files, string? BlockedReason)
{
    public static BundleExtractionResult Extracted(IReadOnlyList<ExtractedBundleFile> files) => new(false, files, null);

    public static BundleExtractionResult Blocked(string reason) => new(true, [], reason);
}

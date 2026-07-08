using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal sealed record BundleExtractionResult(bool IsBlocked, IReadOnlyList<ExtractedBundleFile> Files, string? BlockedReason)
{
    public static BundleExtractionResult Extracted(IReadOnlyList<ExtractedBundleFile> files) => new(false, files, null);

    public static BundleExtractionResult Blocked(string reason) => new(true, [], reason);
}

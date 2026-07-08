using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed partial class BundleFileExtractor
{
    public BundleExtractionResult Extract(string markdown) =>
        Extract(markdown, BundleExtractionPolicy.RoadmapBundle);

    public BundleExtractionResult Extract(string markdown, BundleExtractionPolicy policy)
    {
        MatchCollection matches = FileMarkerRegex().Matches(markdown);
        if (matches.Count == 0)
        {
            return BundleExtractionResult.Blocked("No FILE markers were found in the bundle output.");
        }

        var files = new List<ExtractedBundleFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < matches.Count; index++)
        {
            Match current = matches[index];
            string path = NormalizePath(current.Groups["path"].Value.Trim());
            ValidateTargetPath(path, policy);

            if (!seen.Add(path))
            {
                throw new RoadmapStepException($"Bundle contains duplicate FILE marker for {path}.");
            }

            int contentStart = current.Index + current.Length;
            int contentEnd = index + 1 < matches.Count ? matches[index + 1].Index : markdown.Length;
            string content = TrimSeparatorNoise(markdown[contentStart..contentEnd]);
            files.Add(new ExtractedBundleFile(path, content, RoadmapHash.Sha256(content)));
        }

        return BundleExtractionResult.Extracted(files);
    }

    public async Task WriteExtractedFilesAsync(RoadmapArtifacts artifacts, BundleExtractionResult result)
    {
        if (result.IsBlocked)
        {
            throw new RoadmapStepException($"Cannot write blocked bundle: {result.BlockedReason}");
        }

        foreach (ExtractedBundleFile file in result.Files)
        {
            await artifacts.WriteAsync(file.Path, file.Content);
        }
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static void ValidateTargetPath(string path, BundleExtractionPolicy policy)
    {
        if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal))
        {
            throw new RoadmapStepException($"Bundle target path is not repository-safe: {path}");
        }

        if (!policy.IsAllowed(path))
        {
            throw new RoadmapStepException(policy.RejectionMessage(path));
        }
    }

    private static string TrimSeparatorNoise(string content) => content.Trim('\r', '\n');

    [GeneratedRegex(@"(?m)^# FILE:\s*(?<path>\S+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FileMarkerRegex();
}

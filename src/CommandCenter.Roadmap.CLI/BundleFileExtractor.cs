using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

internal sealed partial class BundleFileExtractor
{
    public BundleExtractionResult Extract(string markdown)
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
            ValidateTargetPath(path);

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

    private static void ValidateTargetPath(string path)
    {
        if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal))
        {
            throw new RoadmapStepException($"Bundle target path is not repository-safe: {path}");
        }

        bool allowed =
            path.StartsWith(".agents/specs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
            path.StartsWith(".agents/epic-", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
            string.Equals(path, RoadmapArtifactPaths.ActiveEpic, StringComparison.Ordinal);

        if (!allowed)
        {
            throw new RoadmapStepException($"Bundle target path is not allowed for roadmap bundle extraction: {path}");
        }
    }

    private static string TrimSeparatorNoise(string content) => content.Trim('\r', '\n');

    [GeneratedRegex(@"(?m)^# FILE:\s*(?<path>\S+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FileMarkerRegex();
}

internal sealed record BundleExtractionResult(bool IsBlocked, IReadOnlyList<ExtractedBundleFile> Files, string? BlockedReason)
{
    public static BundleExtractionResult Extracted(IReadOnlyList<ExtractedBundleFile> files) => new(false, files, null);

    public static BundleExtractionResult Blocked(string reason) => new(true, [], reason);
}

internal sealed record ExtractedBundleFile(string Path, string Content, string Hash);

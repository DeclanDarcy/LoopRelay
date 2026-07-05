using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

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

internal sealed record BundleExtractionResult(bool IsBlocked, IReadOnlyList<ExtractedBundleFile> Files, string? BlockedReason)
{
    public static BundleExtractionResult Extracted(IReadOnlyList<ExtractedBundleFile> files) => new(false, files, null);

    public static BundleExtractionResult Blocked(string reason) => new(true, [], reason);
}

internal sealed record ExtractedBundleFile(string Path, string Content, string Hash);

internal sealed class BundleExtractionPolicy
{
    private readonly Func<string, bool> isAllowed;
    private readonly Func<string, string> rejectionMessage;

    private BundleExtractionPolicy(
        string name,
        Func<string, bool> isAllowed,
        Func<string, string> rejectionMessage)
    {
        Name = name;
        this.isAllowed = isAllowed;
        this.rejectionMessage = rejectionMessage;
    }

    public string Name { get; }

    public static BundleExtractionPolicy RoadmapBundle { get; } = new(
        nameof(RoadmapBundle),
        IsRoadmapBundleTarget,
        path => $"Bundle target path is not allowed for roadmap bundle extraction: {path}");

    public static BundleExtractionPolicy RepositorySafe { get; } = new(
        nameof(RepositorySafe),
        _ => true,
        path => $"Bundle target path is not allowed for repository-safe extraction: {path}");

    public bool IsAllowed(string path) => isAllowed(path);

    public string RejectionMessage(string path) => rejectionMessage(path);

    private static bool IsRoadmapBundleTarget(string path) =>
        path.StartsWith(".agents/specs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
        path.StartsWith(".agents/epic-", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
        string.Equals(path, RoadmapArtifactPaths.ActiveEpic, StringComparison.Ordinal);
}

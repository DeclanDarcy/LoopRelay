using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.ArtifactBundles;

internal sealed class BundleExtractionPolicy
{
    private readonly Func<string, bool> _isAllowed;
    private readonly Func<string, string> _rejectionMessage;

    private BundleExtractionPolicy(
        string name,
        Func<string, bool> isAllowed,
        Func<string, string> rejectionMessage)
    {
        Name = name;
        _isAllowed = isAllowed;
        _rejectionMessage = rejectionMessage;
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

    public bool IsAllowed(string path) => _isAllowed(path);

    public string RejectionMessage(string path) => _rejectionMessage(path);

    private static bool IsRoadmapBundleTarget(string path) =>
        path.StartsWith(".agents/specs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
        path.StartsWith(".agents/epic-", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal) ||
        string.Equals(path, RoadmapArtifactPaths.ActiveEpic, StringComparison.Ordinal);
}

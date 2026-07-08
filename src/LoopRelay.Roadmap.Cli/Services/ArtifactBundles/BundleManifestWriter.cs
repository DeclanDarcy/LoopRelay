using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.ArtifactBundles;

internal sealed class BundleManifestWriter(RoadmapArtifacts _artifacts)
{
    public async Task<string> WriteAsync(
        string manifestPath,
        string sourcePrompt,
        string projectionPath,
        BundleExtractionResult result,
        string validationResult)
    {
        var lines = new List<string>
        {
            "# Bundle Manifest",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Source Prompt | {sourcePrompt} |",
            $"| Projection | {projectionPath} |",
            $"| Expected File Count | {result.Files.Count} |",
            $"| Validation Result | {validationResult} |",
            string.Empty,
            "## Extracted Files",
            string.Empty,
            "| Path | SHA-256 |",
            "|---|---|",
        };

        foreach (ExtractedBundleFile file in result.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            lines.Add($"| {file.Path} | {file.Hash} |");
        }

        await _artifacts.WriteAsync(manifestPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return manifestPath;
    }

    public static string DefaultManifestPath(IReadOnlyList<ExtractedBundleFile> files)
    {
        if (files.Count == 0)
        {
            return ".agents/artifacts/bundle-manifest.md";
        }

        string? directory = Path.GetDirectoryName(files[0].Path.Replace('/', Path.DirectorySeparatorChar));
        return (directory ?? ".agents").Replace(Path.DirectorySeparatorChar, '/') + "/bundle-manifest.md";
    }
}

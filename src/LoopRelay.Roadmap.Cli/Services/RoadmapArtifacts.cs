using System.Text.RegularExpressions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Roadmap.Cli;

internal sealed partial class RoadmapArtifacts(IArtifactStore store, Repository repository)
{
    public Repository Repository => repository;

    public Task<bool> ExistsAsync(string relativePath) => store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) => store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) => store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) => store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> files = await store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return files.Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path)).ToList();
    }

    public async Task<string> ReadRequiredAsync(string relativePath)
    {
        string? content = await ReadAsync(relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new RoadmapStepException($"Required artifact is missing or empty: {relativePath}");
        }

        return content;
    }

    public async Task<string> ReadRoadmapSourceAsync()
    {
        IReadOnlyList<string> sourcePaths = await RequireRoadmapSourcePathsAsync();
        var parts = new List<string>();
        foreach (string file in sourcePaths)
        {
            parts.Add(await ReadRequiredAsync(file));
        }

        return string.Join("\n\n", parts);
    }

    public async Task<IReadOnlyList<string>> RequireRoadmapSourcePathsAsync()
    {
        IReadOnlyList<string> sourcePaths = await ListRoadmapSourcePathsAsync();
        if (sourcePaths.Count == 0)
        {
            throw new RoadmapStepException(
                $"No roadmap source found at {RoadmapArtifactPaths.RoadmapDirectoryPattern}.");
        }

        foreach (string path in sourcePaths)
        {
            _ = await ReadRequiredAsync(path);
        }

        return sourcePaths;
    }

    public async Task<IReadOnlyList<string>> ListRoadmapSourcePathsAsync()
    {
        IReadOnlyList<string> roadmapFiles = await ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        return roadmapFiles.Order(StringComparer.Ordinal).ToArray();
    }

    public async Task<string> WriteNumberedEvidenceAsync(string evidenceDirectory, string stem, string content)
    {
        string path = await NextNumberedPathAsync(evidenceDirectory, stem);
        await WriteAsync(path, content);
        return path;
    }

    public async Task<string> NextNumberedPathAsync(string evidenceDirectory, string stem)
    {
        IReadOnlyList<string> existing = await ListAsync(evidenceDirectory, $"{stem}.*.md");
        int max = 0;
        foreach (string path in existing)
        {
            Match match = NumberedEvidenceRegex().Match(Path.GetFileName(path));
            if (match.Success && int.TryParse(match.Groups["number"].Value, out int number))
            {
                max = Math.Max(max, number);
            }
        }

        return $"{evidenceDirectory}/{stem}.{max + 1:0000}.md";
    }

    public async Task<ArtifactStatus> GetStatusAsync(string relativePath)
    {
        string? content = await ReadAsync(relativePath);
        if (content is null)
        {
            return ArtifactStatus.Missing;
        }

        return string.IsNullOrWhiteSpace(content) ? ArtifactStatus.Empty : ArtifactStatus.Present;
    }

    public string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    [GeneratedRegex(@"\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedEvidenceRegex();
}

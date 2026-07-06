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
        var parts = new List<string>();
        if (await ExistsAsync(RoadmapArtifactPaths.RoadmapFile))
        {
            parts.Add(await ReadRequiredAsync(RoadmapArtifactPaths.RoadmapFile));
        }

        IReadOnlyList<string> roadmapFiles = await ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        foreach (string file in roadmapFiles.Order(StringComparer.Ordinal))
        {
            parts.Add(await ReadRequiredAsync(file));
        }

        if (parts.Count == 0)
        {
            throw new RoadmapStepException(
                $"No roadmap source found at {RoadmapArtifactPaths.RoadmapFile} or {RoadmapArtifactPaths.RoadmapDirectory}/*.md.");
        }

        return string.Join("\n\n", parts);
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

internal enum ArtifactStatus
{
    Missing,
    Empty,
    Present,
}

using System.Text.RegularExpressions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.Persistence;

namespace LoopRelay.Roadmap.Cli.Services.Artifacts;

internal sealed partial class RoadmapArtifacts(
    IArtifactStore _store,
    Repository _repository,
    IExecutionEvidenceStore? executionEvidenceStore = null,
    IWorkflowPersistenceCoordinator? workflowCoordinator = null)
{
    private readonly IExecutionEvidenceStore _executionEvidenceStore =
        executionEvidenceStore ?? new FileBackedExecutionEvidenceStore(_store, _repository);
    private readonly IWorkflowPersistenceCoordinator _workflowCoordinator =
        workflowCoordinator ?? NullWorkflowPersistenceCoordinator.Instance;

    public Repository Repository => _repository;

    public IArtifactStore Store => _store;

    public IExecutionEvidenceStore ExecutionEvidenceStore => _executionEvidenceStore;

    public Task<bool> ExistsAsync(string relativePath) => _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) => _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) => _store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) => _store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        if (IsExecutionEvidenceDirectory(relativeDirectory) &&
            _executionEvidenceStore is SqliteExecutionEvidenceStore)
        {
            return (await _executionEvidenceStore.ListAsync(searchPattern))
                .Select(record => record.RelativePath)
                .ToArray();
        }

        IReadOnlyList<string> files = await _store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return files.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToList();
    }

    public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
    {
        IReadOnlyList<string> directories = await _store.ListDirectoriesAsync(Resolve(relativeDirectory));
        return directories.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToArray();
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
        string? path = null;
        await _workflowCoordinator.ExecuteAsync(
            _repository,
            WorkflowPersistenceUnit.LoopHistoryEvidenceWrite,
            $"{evidenceDirectory}:{stem}",
            async _ => path = await WriteNumberedEvidenceCoreAsync(evidenceDirectory, stem, content));
        return path ?? throw new InvalidOperationException("Numbered evidence workflow did not produce a path.");
    }

    private async Task<string> WriteNumberedEvidenceCoreAsync(string evidenceDirectory, string stem, string content)
    {
        if (IsExecutionEvidenceDirectory(evidenceDirectory))
        {
            return (await _executionEvidenceStore.WriteAsync(stem, content)).RelativePath;
        }

        string path = await NextNumberedPathAsync(evidenceDirectory, stem);
        await WriteAsync(path, content);
        return path;
    }

    public async Task<string> NextNumberedPathAsync(string evidenceDirectory, string stem)
    {
        if (IsExecutionEvidenceDirectory(evidenceDirectory))
        {
            return await _executionEvidenceStore.NextPathAsync(stem);
        }

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

    public string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private static bool IsExecutionEvidenceDirectory(string evidenceDirectory) =>
        string.Equals(
            evidenceDirectory.Replace('\\', '/').TrimEnd('/'),
            FileBackedExecutionEvidenceStore.ExecutionEvidenceDirectory,
            StringComparison.Ordinal);

    [GeneratedRegex(@"\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedEvidenceRegex();
}

using System.Text.RegularExpressions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed partial class CompletionArtifacts(
    IArtifactStore _store,
    Repository _repository,
    IExecutionEvidenceStore? executionEvidenceStore = null)
{
    private readonly IExecutionEvidenceStore _executionEvidenceStore =
        executionEvidenceStore ?? new FileBackedExecutionEvidenceStore(_store, _repository);

    public Repository Repository => _repository;

    public IExecutionEvidenceStore ExecutionEvidenceStore => _executionEvidenceStore;

    public Task<bool> ExistsAsync(string relativePath) => _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) => _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) => _store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) => _store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return files.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToArray();
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
            throw new CompletionCertificationException($"Required artifact is missing or empty: {relativePath}");
        }

        return content;
    }

    public async Task<string> WriteNumberedEvidenceAsync(string evidenceDirectory, string stem, string content)
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

    public async Task MoveFileIfPresentAsync(string sourceRelativePath, string targetRelativePath)
    {
        string? content = await ReadAsync(sourceRelativePath);
        if (content is null)
        {
            return;
        }

        if (await ExistsAsync(targetRelativePath))
        {
            throw new CompletionCertificationException($"Archive target already exists: {targetRelativePath}");
        }

        await WriteAsync(targetRelativePath, content);
        await DeleteAsync(sourceRelativePath);
    }

    public async Task CopyFileIfPresentAsync(string sourceRelativePath, string targetRelativePath)
    {
        string? content = await ReadAsync(sourceRelativePath);
        if (content is null)
        {
            return;
        }

        if (await ExistsAsync(targetRelativePath))
        {
            throw new CompletionCertificationException($"Archive target already exists: {targetRelativePath}");
        }

        await WriteAsync(targetRelativePath, content);
    }

    public async Task MoveDirectoryContentsAsync(string sourceDirectory, string targetDirectory)
    {
        IReadOnlyList<string> files = await ListAsync(sourceDirectory, "*");
        foreach (string sourcePath in files.Order(StringComparer.Ordinal))
        {
            string relativeSuffix = RelativeSuffix(sourceDirectory, sourcePath);
            await MoveFileIfPresentAsync(sourcePath, Join(targetDirectory, relativeSuffix));
        }
    }

    public string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private static bool IsExecutionEvidenceDirectory(string evidenceDirectory) =>
        string.Equals(
            evidenceDirectory.Replace('\\', '/').TrimEnd('/'),
            FileBackedExecutionEvidenceStore.ExecutionEvidenceDirectory,
            StringComparison.Ordinal);

    private static string RelativeSuffix(string sourceDirectory, string sourcePath)
    {
        string normalizedDirectory = Normalize(sourceDirectory).TrimEnd('/');
        string normalizedPath = Normalize(sourcePath);
        if (!normalizedPath.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalizedPath);
        }

        return normalizedPath[(normalizedDirectory.Length + 1)..];
    }

    private static string Join(string left, string right) =>
        $"{Normalize(left).TrimEnd('/')}/{Normalize(right).TrimStart('/')}";

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    [GeneratedRegex(@"\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedEvidenceRegex();
}

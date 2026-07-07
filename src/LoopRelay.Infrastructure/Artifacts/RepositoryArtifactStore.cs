using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Infrastructure.Artifacts;

/// <summary>Repository-relative facade over an artifact store.</summary>
public sealed class RepositoryArtifactStore(IArtifactStore store, Repository repository) : IArtifactStore
{
    public Task<bool> ExistsAsync(string relativePath) =>
        store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) =>
        store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> absolutePaths = await store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
    {
        IReadOnlyList<string> absolutePaths = await store.ListDirectoriesAsync(Resolve(relativeDirectory));
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);
}

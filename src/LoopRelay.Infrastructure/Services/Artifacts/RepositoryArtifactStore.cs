using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;

namespace LoopRelay.Infrastructure.Services.Artifacts;

/// <summary>Repository-relative facade over an artifact store.</summary>
public sealed class RepositoryArtifactStore(IArtifactStore _store, Repository _repository) : IArtifactStore
{
    public Task<bool> ExistsAsync(string relativePath) =>
        _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        _store.WriteAsync(Resolve(relativePath), content);

    public Task DeleteAsync(string relativePath) =>
        _store.DeleteAsync(Resolve(relativePath));

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> absolutePaths = await _store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
    {
        IReadOnlyList<string> absolutePaths = await _store.ListDirectoriesAsync(Resolve(relativeDirectory));
        return absolutePaths
            .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);
}

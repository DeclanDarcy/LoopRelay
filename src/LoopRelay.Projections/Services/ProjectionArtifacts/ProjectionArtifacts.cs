using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;

namespace LoopRelay.Projections.Services.ProjectionArtifacts;

public sealed class ProjectionArtifacts(IArtifactStore _store, Repository _repository)
{
    public Repository Repository => _repository;

    public Task<bool> ExistsAsync(string relativePath) => _store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) => _store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) => _store.WriteAsync(Resolve(relativePath), content);

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> files = await _store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return files.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToList();
    }

    public string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(_repository, relativePath);
}

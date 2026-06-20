using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Artifacts;

public interface IArtifactService
{
    Task<IReadOnlyList<Artifact>> DiscoverAsync(Repository repository);

    Task<Artifact?> GetCurrentHandoffAsync(Repository repository);

    Task<Artifact?> GetCurrentOperationalContextAsync(Repository repository);

    Task<Artifact?> GetCurrentDecisionsAsync(Repository repository);

    Task<bool> ExistsAsync(Repository repository, string relativePath);

    Task<string> LoadAsync(Repository repository, string relativePath);

    Task SaveAsync(Repository repository, string relativePath, string content);
}

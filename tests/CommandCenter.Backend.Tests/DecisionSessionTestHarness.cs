using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.Backend.Tests;

internal sealed record DecisionSessionTestHarness(
    Repository Repository,
    MemoryArtifactStore Store,
    DecisionSessionTestRepositoryService RepositoryService,
    FileSystemDecisionSessionRepository RepositoryStore,
    DecisionSessionRegistry Registry,
    DecisionSessionRecoveryService Recovery)
{
    public static DecisionSessionTestHarness Create()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"))
        };
        Directory.CreateDirectory(repository.Path);
        var store = new MemoryArtifactStore();
        var repositoryService = new DecisionSessionTestRepositoryService(repository);
        var sessionRepository = new FileSystemDecisionSessionRepository(store);
        var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
        var recovery = new DecisionSessionRecoveryService(repositoryService, sessionRepository);
        return new DecisionSessionTestHarness(repository, store, repositoryService, sessionRepository, registry, recovery);
    }

    public static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task WriteRegistryAsync(
        IReadOnlyList<DecisionSession> sessions,
        Guid? documentRepositoryId = null,
        string schemaVersion = DecisionSessionArtifactPaths.SchemaVersion)
    {
        var document = new DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>(
            schemaVersion,
            documentRepositoryId ?? Repository.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            sessions.Select(session => new DecisionSessionRecord(session)).ToArray());
        await Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(Repository, DecisionSessionArtifactPaths.RegistryJson()),
            JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }
}

internal sealed class DecisionSessionTestRepositoryService(params Repository[] repositories) : IRepositoryService
{
    public Task<IReadOnlyList<Repository>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<Repository>>(repositories);
    }

    public Task<Repository> RegisterAsync(string repositoryPath)
    {
        throw new NotSupportedException();
    }

    public Task RemoveAsync(Guid repositoryId)
    {
        throw new NotSupportedException();
    }
}

using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class ArtifactRotationServiceTests
{
    [Fact]
    public async Task FirstAndSecondHandoffRotationsCreateSequentialHistoricalFiles()
    {
        var repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        var service = CreateRotationService();

        var first = await service.RotateCurrentHandoffAsync(repository);
        var second = await service.RotateCurrentHandoffAsync(repository);

        Assert.Equal(".agents/handoffs/handoff.0001.md", first.RelativePath);
        Assert.Equal(".agents/handoffs/handoff.0002.md", second.RelativePath);
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.md"));
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.0002.md"));
    }

    [Fact]
    public async Task FirstAndSecondDecisionRotationsCreateSequentialHistoricalFiles()
    {
        var repository = CreateRepository();
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");
        var service = CreateRotationService();

        var first = await service.RotateCurrentDecisionsAsync(repository);
        var second = await service.RotateCurrentDecisionsAsync(repository);

        Assert.Equal(".agents/decisions/decisions.0001.md", first.RelativePath);
        Assert.Equal(".agents/decisions/decisions.0002.md", second.RelativePath);
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.md"));
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.0001.md"));
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.0002.md"));
    }

    [Fact]
    public async Task RotationPreservesExistingHistoricalFiles()
    {
        var repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "current");
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "first historical");
        var service = CreateRotationService();

        var rotated = await service.RotateCurrentHandoffAsync(repository);

        Assert.Equal(".agents/handoffs/handoff.0002.md", rotated.RelativePath);
        Assert.Equal("first historical", await ReadAsync(repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("current", await ReadAsync(repository, ".agents/handoffs/handoff.0002.md"));
    }

    [Fact]
    public async Task RotationFailsWhenCurrentArtifactIsMissing()
    {
        var repository = CreateRepository();
        var service = CreateRotationService();

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.RotateCurrentHandoffAsync(repository));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.RotateCurrentDecisionsAsync(repository));
    }

    [Fact]
    public async Task RotationFailsForUnsupportedFamilies()
    {
        var repository = CreateRepository();
        var service = CreateRotationService();

        await Assert.ThrowsAsync<NotSupportedException>(() => service.RotateAsync(repository, ArtifactFamily.Plan));
    }

    [Fact]
    public async Task RotationFailsInsteadOfOverwritingTarget()
    {
        var repository = CreateRepository();
        var currentPath = Path.Combine(repository.Path, ".agents", "handoffs", "handoff.md");
        var targetPath = Path.Combine(repository.Path, ".agents", "handoffs", "handoff.0001.md");
        var store = new CollisionArtifactStore(new FileSystemArtifactStore(), targetPath);
        await store.WriteAsync(currentPath, "handoff");
        var artifactService = new ArtifactService(store);
        var service = new ArtifactRotationService(store, artifactService);

        await Assert.ThrowsAsync<IOException>(() => service.RotateCurrentHandoffAsync(repository));
    }

    [Fact]
    public async Task RefreshAfterRotationUpdatesArtifactInventory()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        var artifactStore = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(artifactStore);
        var rotationService = new ArtifactRotationService(artifactStore, artifactService);
        var projectionService = new RepositoryProjectionService(
            repositoryService,
            artifactService,
            new PlanningService(new FileSystemArtifactStore()),
            new ReadyExecutionSessionService());

        var beforeRotation = await projectionService.GetWorkspaceAsync(repository.Id);
        await rotationService.RotateCurrentHandoffAsync(repository);
        var afterRotation = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.Empty(beforeRotation.ArtifactInventory.HistoricalHandoffs);
        var historical = Assert.Single(afterRotation.ArtifactInventory.HistoricalHandoffs);
        Assert.Equal(".agents/handoffs/handoff.0001.md", historical.RelativePath);
        Assert.NotNull(afterRotation.ArtifactInventory.CurrentHandoff);
    }

    private static ArtifactRotationService CreateRotationService()
    {
        var store = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(store);
        return new ArtifactRotationService(store, artifactService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        var path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static Repository CreateRepository()
    {
        var directory = CreateGitRepositoryDirectory();

        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(directory),
            Path = directory
        };
    }

    private static string CreateGitRepositoryDirectory()
    {
        var directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CollisionArtifactStore(IArtifactStore innerStore, string collisionPath) : IArtifactStore
    {
        public Task<bool> ExistsAsync(string path)
        {
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(collisionPath), StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(true)
                : innerStore.ExistsAsync(path);
        }

        public Task<string?> ReadAsync(string path)
        {
            return innerStore.ReadAsync(path);
        }

        public Task WriteAsync(string path, string content)
        {
            return innerStore.WriteAsync(path, content);
        }

        public Task DeleteAsync(string path)
        {
            return innerStore.DeleteAsync(path);
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
        {
            return innerStore.ListAsync(path, searchPattern);
        }
    }

    private sealed class ReadyExecutionSessionService : IExecutionSessionService
    {
        public Task RecoverAsync()
        {
            return Task.CompletedTask;
        }

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
        {
            return Task.FromResult(RepositoryExecutionState.Ready);
        }

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(null);
        }

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(null);
        }

        public Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid repositoryId, int limit = 10)
        {
            return Task.FromResult<IReadOnlyList<ExecutionSessionSummary>>([]);
        }

        public Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<ExecutionSession?>(null);
        }

        public Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
        {
            throw new NotSupportedException();
        }
    }
}

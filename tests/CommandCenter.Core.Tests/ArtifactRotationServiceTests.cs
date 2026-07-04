using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Tests;

public sealed class ArtifactRotationServiceTests
{
    [Fact]
    public async Task FirstAndSecondHandoffRotationsCreateSequentialHistoricalFiles()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        ArtifactRotationService service = CreateRotationService();

        Artifact first = await service.RotateCurrentHandoffAsync(repository);
        Artifact second = await service.RotateCurrentHandoffAsync(repository);

        Assert.Equal(".agents/handoffs/handoff.0001.md", first.RelativePath);
        Assert.Equal(".agents/handoffs/handoff.0002.md", second.RelativePath);
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.md"));
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("handoff", await ReadAsync(repository, ".agents/handoffs/handoff.0002.md"));
    }

    [Fact]
    public async Task FirstAndSecondDecisionRotationsCreateSequentialHistoricalFiles()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");
        ArtifactRotationService service = CreateRotationService();

        Artifact first = await service.RotateCurrentDecisionsAsync(repository);
        Artifact second = await service.RotateCurrentDecisionsAsync(repository);

        Assert.Equal(".agents/decisions/decisions.0001.md", first.RelativePath);
        Assert.Equal(".agents/decisions/decisions.0002.md", second.RelativePath);
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.md"));
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.0001.md"));
        Assert.Equal("decisions", await ReadAsync(repository, ".agents/decisions/decisions.0002.md"));
    }

    [Fact]
    public async Task RotationPreservesExistingHistoricalFiles()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "current");
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "first historical");
        ArtifactRotationService service = CreateRotationService();

        Artifact rotated = await service.RotateCurrentHandoffAsync(repository);

        Assert.Equal(".agents/handoffs/handoff.0002.md", rotated.RelativePath);
        Assert.Equal("first historical", await ReadAsync(repository, ".agents/handoffs/handoff.0001.md"));
        Assert.Equal("current", await ReadAsync(repository, ".agents/handoffs/handoff.0002.md"));
    }

    [Fact]
    public async Task RotationFailsWhenCurrentArtifactIsMissing()
    {
        Repository repository = CreateRepository();
        ArtifactRotationService service = CreateRotationService();

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.RotateCurrentHandoffAsync(repository));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.RotateCurrentDecisionsAsync(repository));
    }

    [Fact]
    public async Task RotationFailsForUnsupportedFamilies()
    {
        Repository repository = CreateRepository();
        ArtifactRotationService service = CreateRotationService();

        await Assert.ThrowsAsync<NotSupportedException>(() => service.RotateAsync(repository, ArtifactFamily.Plan));
    }

    [Fact]
    public async Task RotationFailsInsteadOfOverwritingTarget()
    {
        Repository repository = CreateRepository();
        string currentPath = Path.Combine(repository.Path, ".agents", "handoffs", "handoff.md");
        string targetPath = Path.Combine(repository.Path, ".agents", "handoffs", "handoff.0001.md");
        var store = new CollisionArtifactStore(new FileSystemArtifactStore(), targetPath);
        await store.WriteAsync(currentPath, "handoff");
        var artifactService = new ArtifactService(store);
        var service = new ArtifactRotationService(store, artifactService);

        await Assert.ThrowsAsync<IOException>(() => service.RotateCurrentHandoffAsync(repository));
    }

    private static ArtifactRotationService CreateRotationService()
    {
        var store = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(store);
        return new ArtifactRotationService(store, artifactService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
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
        string directory = CreateGitRepositoryDirectory();

        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(directory),
            Path = directory
        };
    }

    private static string CreateGitRepositoryDirectory()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path)
        {
            return innerStore.ListDirectoriesAsync(path);
        }
    }
}

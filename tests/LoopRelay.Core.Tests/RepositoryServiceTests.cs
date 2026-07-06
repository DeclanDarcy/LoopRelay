using LoopRelay.Core.Configuration;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Core.Tests;

public sealed class RepositoryServiceTests
{
    [Fact]
    public async Task ValidRepositoryRegistersSuccessfully()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService service = CreateService();

        Repository repository = await service.RegisterAsync(repositoryPath);

        Assert.NotEqual(Guid.Empty, repository.Id);
        Assert.Equal(new DirectoryInfo(repositoryPath).Name, repository.Name);
        Assert.Equal(Path.GetFullPath(repositoryPath).TrimEnd(Path.DirectorySeparatorChar), repository.Path);
    }

    [Fact]
    public async Task InvalidNonDirectoryPathFails()
    {
        string directory = CreateTemporaryDirectory();
        string filePath = Path.Combine(directory, "not-a-directory");
        await File.WriteAllTextAsync(filePath, "");
        RepositoryService service = CreateService();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.RegisterAsync(filePath));
    }

    [Fact]
    public async Task DirectoryWithoutGitFails()
    {
        string directory = CreateTemporaryDirectory();
        RepositoryService service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(directory));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForEquivalentPaths()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService service = CreateService();
        await service.RegisterAsync(repositoryPath);
        string equivalentPath = Path.Combine(repositoryPath, ".");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(equivalentPath));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForCaseDifferences()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService service = CreateService();
        await service.RegisterAsync(repositoryPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(repositoryPath.ToUpperInvariant()));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForMixedSeparators()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService service = CreateService();
        await service.RegisterAsync(repositoryPath);
        string mixedSeparatorPath = repositoryPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(mixedSeparatorPath));
    }

    [Fact]
    public async Task AgentsDirectoryIsCreatedWhenMissing()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        RepositoryService service = CreateService();

        await service.RegisterAsync(repositoryPath);

        Assert.True(Directory.Exists(Path.Combine(repositoryPath, ".agents")));
    }

    [Fact]
    public async Task ExistingAgentsDirectoryIsNotModified()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        string agentsPath = Path.Combine(repositoryPath, ".agents");
        Directory.CreateDirectory(agentsPath);
        string sentinelPath = Path.Combine(agentsPath, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "keep");
        RepositoryService service = CreateService();

        await service.RegisterAsync(repositoryPath);

        Assert.Equal("keep", await File.ReadAllTextAsync(sentinelPath));
    }

    [Fact]
    public async Task RegisteredRepositoriesSurviveConfigurationReload()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        string configurationPath = CreateConfigurationPath();
        RepositoryService service = CreateService(configurationPath);
        Repository repository = await service.RegisterAsync(repositoryPath);

        IReadOnlyList<Repository> reloaded = await CreateService(configurationPath).GetAllAsync();

        Repository loadedRepository = Assert.Single(reloaded);
        Assert.Equal(repository.Id, loadedRepository.Id);
        Assert.Equal(repository.Name, loadedRepository.Name);
        Assert.Equal(repository.Path, loadedRepository.Path);
    }

    [Fact]
    public async Task RemovingRepositoryUpdatesConfigurationAndLeavesFilesUntouched()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        string trackedFile = Path.Combine(repositoryPath, "tracked.txt");
        await File.WriteAllTextAsync(trackedFile, "content");
        RepositoryService service = CreateService();
        Repository repository = await service.RegisterAsync(repositoryPath);

        await service.RemoveAsync(repository.Id);

        Assert.Empty(await service.GetAllAsync());
        Assert.True(Directory.Exists(repositoryPath));
        Assert.True(Directory.Exists(Path.Combine(repositoryPath, ".git")));
        Assert.True(Directory.Exists(Path.Combine(repositoryPath, ".agents")));
        Assert.Equal("content", await File.ReadAllTextAsync(trackedFile));
    }

    private static RepositoryService CreateService(string? configurationPath = null)
    {
        return new RepositoryService(new ApplicationConfigurationStore(configurationPath ?? CreateConfigurationPath()));
    }

    private static string CreateGitRepositoryDirectory()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateConfigurationPath()
    {
        return Path.Combine(CreateTemporaryDirectory(), "configuration.json");
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class RepositoryServiceTests
{
    [Fact]
    public async Task ValidRepositoryRegistersSuccessfully()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var service = CreateService();

        var repository = await service.RegisterAsync(repositoryPath);

        Assert.NotEqual(Guid.Empty, repository.Id);
        Assert.Equal(new DirectoryInfo(repositoryPath).Name, repository.Name);
        Assert.Equal(Path.GetFullPath(repositoryPath).TrimEnd(Path.DirectorySeparatorChar), repository.Path);
    }

    [Fact]
    public async Task InvalidNonDirectoryPathFails()
    {
        var directory = CreateTemporaryDirectory();
        var filePath = Path.Combine(directory, "not-a-directory");
        await File.WriteAllTextAsync(filePath, "");
        var service = CreateService();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.RegisterAsync(filePath));
    }

    [Fact]
    public async Task DirectoryWithoutGitFails()
    {
        var directory = CreateTemporaryDirectory();
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(directory));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForEquivalentPaths()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var service = CreateService();
        await service.RegisterAsync(repositoryPath);
        var equivalentPath = Path.Combine(repositoryPath, ".");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(equivalentPath));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForCaseDifferences()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var service = CreateService();
        await service.RegisterAsync(repositoryPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(repositoryPath.ToUpperInvariant()));
    }

    [Fact]
    public async Task DuplicateRegistrationFailsForMixedSeparators()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var service = CreateService();
        await service.RegisterAsync(repositoryPath);
        var mixedSeparatorPath = repositoryPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(mixedSeparatorPath));
    }

    [Fact]
    public async Task AgentsDirectoryIsCreatedWhenMissing()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var service = CreateService();

        await service.RegisterAsync(repositoryPath);

        Assert.True(Directory.Exists(Path.Combine(repositoryPath, ".agents")));
    }

    [Fact]
    public async Task ExistingAgentsDirectoryIsNotModified()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var agentsPath = Path.Combine(repositoryPath, ".agents");
        Directory.CreateDirectory(agentsPath);
        var sentinelPath = Path.Combine(agentsPath, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "keep");
        var service = CreateService();

        await service.RegisterAsync(repositoryPath);

        Assert.Equal("keep", await File.ReadAllTextAsync(sentinelPath));
    }

    [Fact]
    public async Task RegisteredRepositoriesSurviveConfigurationReload()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var configurationPath = CreateConfigurationPath();
        var service = CreateService(configurationPath);
        var repository = await service.RegisterAsync(repositoryPath);

        var reloaded = await CreateService(configurationPath).GetAllAsync();

        var loadedRepository = Assert.Single(reloaded);
        Assert.Equal(repository.Id, loadedRepository.Id);
        Assert.Equal(repository.Name, loadedRepository.Name);
        Assert.Equal(repository.Path, loadedRepository.Path);
    }

    [Fact]
    public async Task RemovingRepositoryUpdatesConfigurationAndLeavesFilesUntouched()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var trackedFile = Path.Combine(repositoryPath, "tracked.txt");
        await File.WriteAllTextAsync(trackedFile, "content");
        var service = CreateService();
        var repository = await service.RegisterAsync(repositoryPath);

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
        var directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateConfigurationPath()
    {
        return Path.Combine(CreateTemporaryDirectory(), "configuration.json");
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

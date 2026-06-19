using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class RepositoryProjectionServiceTests
{
    [Fact]
    public async Task DashboardReturnsRegisteredRepositoryAvailability()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var projectionService = new RepositoryProjectionService(repositoryService);

        var dashboard = await projectionService.GetDashboardAsync();

        var projection = Assert.Single(dashboard);
        Assert.Equal(repository.Id, projection.Repository.Id);
        Assert.Equal(RepositoryAvailability.Available, projection.Availability);
    }

    [Fact]
    public async Task MissingRegisteredRepositoryReportsMissing()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        Directory.Delete(repositoryPath, recursive: true);
        var projectionService = new RepositoryProjectionService(repositoryService);

        var dashboard = await projectionService.GetDashboardAsync();

        var projection = Assert.Single(dashboard);
        Assert.Equal(repository.Id, projection.Repository.Id);
        Assert.Equal(RepositoryAvailability.Missing, projection.Availability);
    }

    private static RepositoryService CreateRepositoryService()
    {
        return new RepositoryService(new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
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
}

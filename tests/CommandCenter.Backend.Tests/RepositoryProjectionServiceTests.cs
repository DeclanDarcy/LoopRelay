using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Planning;
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
        var projectionService = CreateProjectionService(repositoryService);

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
        var projectionService = CreateProjectionService(repositoryService);

        var dashboard = await projectionService.GetDashboardAsync();

        var projection = Assert.Single(dashboard);
        Assert.Equal(repository.Id, projection.Repository.Id);
        Assert.Equal(RepositoryAvailability.Missing, projection.Availability);
    }

    [Fact]
    public async Task WorkspaceRefreshDiscoversExternallyAddedArtifacts()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var projectionService = CreateProjectionService(repositoryService);

        var beforeRefresh = await projectionService.GetWorkspaceAsync(repository.Id);
        await WriteAsync(repository, ".agents/milestones/m2.md", "milestone");
        var afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.Empty(beforeRefresh.ArtifactInventory.Milestones);
        var milestone = Assert.Single(afterRefresh.ArtifactInventory.Milestones);
        Assert.Equal(".agents/milestones/m2.md", milestone.RelativePath);
    }

    [Fact]
    public async Task WorkspaceRefreshRemovesExternallyDeletedArtifacts()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        var projectionService = CreateProjectionService(repositoryService);

        var beforeDelete = await projectionService.GetWorkspaceAsync(repository.Id);
        File.Delete(Path.Combine(repository.Path, ".agents", "handoffs", "handoff.md"));
        var afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.NotNull(beforeDelete.ArtifactInventory.CurrentHandoff);
        Assert.Null(afterRefresh.ArtifactInventory.CurrentHandoff);
        Assert.False(afterRefresh.HasCurrentHandoff);
    }

    [Fact]
    public async Task WorkspaceProjectionComposesInventoryStatus()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/operational_context.md", "context");
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");
        var projectionService = CreateProjectionService(repositoryService);

        var workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.True(workspace.HasPlan);
        Assert.True(workspace.HasOperationalContext);
        Assert.True(workspace.HasCurrentDecisions);
        Assert.NotNull(workspace.ArtifactInventory.Plan);
        Assert.NotNull(workspace.ArtifactInventory.OperationalContext);
        Assert.NotNull(workspace.ArtifactInventory.CurrentDecisions);
    }

    private static RepositoryService CreateRepositoryService()
    {
        return new RepositoryService(new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
    }

    private static RepositoryProjectionService CreateProjectionService(IRepositoryService repositoryService)
    {
        return new RepositoryProjectionService(
            repositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService());
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

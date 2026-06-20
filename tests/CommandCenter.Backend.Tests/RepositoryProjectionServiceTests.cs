using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Execution;
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

    [Fact]
    public async Task DashboardAndWorkspaceProjectPlanningReadiness()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        var projectionService = CreateProjectionService(repositoryService);

        var dashboard = await projectionService.GetDashboardAsync();
        var workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        var dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(ExecutionReadiness.Ready, dashboardProjection.Readiness);
        Assert.Equal(1, dashboardProjection.MilestoneCount);
        Assert.Equal(ExecutionReadiness.Ready, workspace.Readiness);
        Assert.True(workspace.HasPlan);
        Assert.Equal(1, workspace.MilestoneCount);
    }

    [Fact]
    public async Task DashboardAndWorkspaceProjectDefaultExecutionState()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var projectionService = CreateProjectionService(repositoryService);

        var dashboard = await projectionService.GetDashboardAsync();
        var workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        var dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.Ready, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.Equal(RepositoryExecutionState.Ready, workspace.ExecutionState);
        Assert.Null(workspace.ExecutionSummary);
    }

    [Fact]
    public async Task DashboardAndWorkspaceProjectAwaitingAcceptanceSummary()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var sessionId = Guid.NewGuid();
        var projectionService = CreateProjectionService(
            repositoryService,
            new StaticExecutionSessionService(
                repository.Id,
                new ExecutionSessionSummary
                {
                    SessionId = sessionId,
                    State = ExecutionSessionState.Completed,
                    RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
                    MilestonePath = ".agents/milestones/m4.md",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
                    CompletedAt = DateTimeOffset.UtcNow,
                    Duration = TimeSpan.FromMinutes(3),
                    LastActivityAt = DateTimeOffset.UtcNow,
                    ProviderName = "fake",
                    HandoffPath = HandoffService.CurrentHandoffPath
                }));

        var dashboard = await projectionService.GetDashboardAsync();
        var workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        var dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.NotNull(dashboardProjection.ExecutionSummary);
        Assert.Equal(sessionId, dashboardProjection.ExecutionSummary.SessionId);
        Assert.Equal(HandoffService.CurrentHandoffPath, dashboardProjection.ExecutionSummary.HandoffPath);
        Assert.Equal(TimeSpan.FromMinutes(3), dashboardProjection.ExecutionSummary.Duration);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, workspace.ExecutionState);
        Assert.NotNull(workspace.ExecutionSummary);
        Assert.Equal(HandoffService.CurrentHandoffPath, workspace.ExecutionSummary.HandoffPath);
        Assert.Equal(TimeSpan.FromMinutes(3), workspace.ExecutionSummary.Duration);
    }

    [Fact]
    public async Task RefreshAfterAddingMilestoneUpdatesReadiness()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = CreateRepositoryService();
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/plan.md", "plan");
        var projectionService = CreateProjectionService(repositoryService);

        var beforeRefresh = await projectionService.GetWorkspaceAsync(repository.Id);
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        var afterRefresh = await projectionService.RefreshWorkspaceAsync(repository.Id);

        Assert.Equal(ExecutionReadiness.MissingMilestones, beforeRefresh.Readiness);
        Assert.Equal(0, beforeRefresh.MilestoneCount);
        Assert.Equal(ExecutionReadiness.Ready, afterRefresh.Readiness);
        Assert.Equal(1, afterRefresh.MilestoneCount);
    }

    private static RepositoryService CreateRepositoryService()
    {
        return new RepositoryService(new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
    }

    private static RepositoryProjectionService CreateProjectionService(IRepositoryService repositoryService)
    {
        return CreateProjectionService(repositoryService, new ReadyExecutionSessionService());
    }

    private static RepositoryProjectionService CreateProjectionService(
        IRepositoryService repositoryService,
        IExecutionSessionService executionSessionService)
    {
        return new RepositoryProjectionService(
            repositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            executionSessionService);
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
    }

    private sealed class StaticExecutionSessionService(
        Guid repositoryId,
        ExecutionSessionSummary summary) : IExecutionSessionService
    {
        public Task RecoverAsync()
        {
            return Task.CompletedTask;
        }

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(requestedRepositoryId == repositoryId
                ? summary.RepositoryState
                : RepositoryExecutionState.Ready);
        }

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(
                requestedRepositoryId == repositoryId && summary.RepositoryState == RepositoryExecutionState.Executing
                    ? summary
                    : null);
        }

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(
                requestedRepositoryId == repositoryId ? summary : null);
        }

        public Task<ExecutionSessionSummary> StartAsync(Guid requestedRepositoryId, ExecutionStartRequest request)
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
    }
}

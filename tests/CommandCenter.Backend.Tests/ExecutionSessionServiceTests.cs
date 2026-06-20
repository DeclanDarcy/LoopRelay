using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionSessionServiceTests
{
    [Fact]
    public async Task ReadyRepositoryLaunchesWithFakeProvider()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        Assert.Equal(ExecutionSessionState.Executing, summary.State);
        Assert.Equal(RepositoryExecutionState.Executing, summary.RepositoryState);
        Assert.Equal("fake", summary.ProviderName);
        Assert.Equal(RepositoryExecutionState.Executing, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task MissingPlanBlocksLaunch()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "milestone");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("launch is blocked", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task MissingMilestoneBlocksLaunch()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/missing.md" }));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task ContextHardLimitBlocksLaunch()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 260 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "milestone");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("hard limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task DirtyRepositoryLaunchSucceedsAndStoresSnapshot()
    {
        var dirtyState = new RepositoryDirtyState
        {
            ModifiedPaths = ["src/changed.cs"],
            IsClean = false
        };
        var harness = await CreateHarnessAsync(dirtyState);
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var session = (await harness.Store.LoadAsync()).Single(session => session.Id == summary.SessionId);

        Assert.False(session.RepositorySnapshot!.DirtyState.IsClean);
        Assert.Contains("src/changed.cs", session.RepositorySnapshot.DirtyState.ModifiedPaths);
    }

    [Fact]
    public async Task DuplicateLaunchBlocks()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);

        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SessionService.StartAsync(
                harness.Repository.Id,
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" }));

        Assert.Contains("active execution", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await harness.Store.LoadAsync());
    }

    [Fact]
    public async Task FakeProviderFailureLeavesRepositoryReadyAndRecordsFailure()
    {
        var provider = new FakeExecutionProvider { FailOnStart = true };
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var session = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Contains("failed to start", session.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveSessionRestoresAfterStoreReload()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider());

        var active = await reloadedService.GetActiveSessionAsync(harness.Repository.Id);

        Assert.NotNull(active);
        Assert.Equal(summary.SessionId, active.SessionId);
        Assert.Equal(RepositoryExecutionState.Executing, await reloadedService.GetRepositoryStateAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task DashboardProjectionRestoresActiveSessionAfterStoreReload()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider());
        var artifactStore = new FileSystemArtifactStore();
        var projectionService = new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            reloadedService);

        var dashboard = await projectionService.GetDashboardAsync();
        var workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);

        var dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.Executing, dashboardProjection.ExecutionState);
        Assert.NotNull(dashboardProjection.ActiveExecutionSession);
        Assert.Equal(summary.SessionId, dashboardProjection.ActiveExecutionSession.SessionId);
        Assert.Equal(RepositoryExecutionState.Executing, workspace.ExecutionState);
        Assert.NotNull(workspace.ExecutionSummary);
        Assert.Equal(summary.SessionId, workspace.ExecutionSummary.SessionId);
    }

    [Fact]
    public async Task PreviousCurrentHandoffSnapshotIsCapturedBeforeProviderStart()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", "previous handoff");

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var session = (await harness.Store.LoadAsync()).Single(session => session.Id == summary.SessionId);

        Assert.Equal("previous handoff", session.PreviousHandoffContent);
        Assert.NotNull(session.PreviousHandoffCapturedAt);
    }

    [Fact]
    public async Task LaunchEndpointReturnsSessionMetadata()
    {
        var configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        var previousStorePath = Environment.GetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);
        Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", storePath);

        try
        {
            var repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            var repository = await repositoryService.RegisterAsync(repositoryPath);
            await WriteReadyArtifactsAsync(repository);

            await using var app = Program.CreateApp(
                [],
                services =>
                {
                    services.AddSingleton<IGitService>(new FakeGitService(null, null));
                    services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                    services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                });
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync(
                app.Urls.Single() + $"/api/repositories/{repository.Id}/execution/start",
                new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var summary = await response.Content.ReadFromJsonAsync<ExecutionSessionSummary>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            });
            Assert.NotNull(summary);
            Assert.Equal(ExecutionSessionState.Executing, summary.State);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
            Environment.SetEnvironmentVariable("COMMAND_CENTER_EXECUTION_SESSIONS_PATH", previousStorePath);
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        RepositoryDirtyState? dirtyState = null,
        string? gitFailure = null,
        FakeExecutionProvider? provider = null)
    {
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());
        var artifactStore = new FileSystemArtifactStore();
        var contextService = new ExecutionContextService(
            repositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            new FakeGitService(dirtyState, gitFailure));
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = new FileSystemExecutionSessionStore(storePath);
        var sessionService = new ExecutionSessionService(
            contextService,
            store,
            provider ?? new FakeExecutionProvider());

        return new Harness(repositoryService, repository, contextService, store, storePath, sessionService);
    }

    private static async Task WriteReadyArtifactsAsync(Repository repository)
    {
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/milestones/m2.md", "milestone");
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

    private sealed record Harness(
        RepositoryService RepositoryService,
        Repository Repository,
        ExecutionContextService ContextService,
        FileSystemExecutionSessionStore Store,
        string StorePath,
        ExecutionSessionService SessionService);

    private sealed class FakeGitService(RepositoryDirtyState? dirtyState, string? failure) : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }
    }
}

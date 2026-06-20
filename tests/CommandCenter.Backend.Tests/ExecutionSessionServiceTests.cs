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
    public async Task StructuredProviderStartFailureLeavesRepositoryReadyAndRecordsFailure()
    {
        var provider = new FailingExecutionProvider(new ExecutionProviderException(
            "ProviderLaunchFailed",
            "Codex process failed to start."));
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var session = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Ready, await harness.SessionService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Contains("ProviderLaunchFailed", session.FailureReason);
    }

    [Fact]
    public async Task StartupRecoveryFailsPersistedExecutingSessionAfterStoreReload()
    {
        var provider = new MetadataExecutionProvider();
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);
        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            new ExecutionMonitoringService(new FileSystemExecutionSessionStore(harness.StorePath)));

        await reloadedService.RecoverAsync();
        var active = await reloadedService.GetActiveSessionAsync(harness.Repository.Id);
        var repositorySummary = await reloadedService.GetRepositorySessionSummaryAsync(harness.Repository.Id);
        var recoveredSession = await reloadedService.GetSessionAsync(summary.SessionId);

        Assert.Null(active);
        Assert.NotNull(repositorySummary);
        Assert.Equal(ExecutionSessionState.Failed, repositorySummary.State);
        Assert.Equal(RepositoryExecutionState.Failed, repositorySummary.RepositoryState);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, repositorySummary.FailureReason);
        Assert.Equal("C:\\tools\\codex.exe", repositorySummary.ProviderExecutablePath);
        Assert.Equal(7890, repositorySummary.ProviderProcessId);
        Assert.NotNull(recoveredSession);
        Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
        Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
        Assert.Equal(RepositoryExecutionState.Failed, await reloadedService.GetRepositoryStateAsync(harness.Repository.Id));
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, recoveredSession.FailureReason);
        Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
        Assert.Equal(7890, recoveredSession.ProviderProcessId);
        Assert.NotNull(recoveredSession.ProviderStartedAt);
        Assert.NotNull(recoveredSession.PromptMetadata);
    }

    [Fact]
    public async Task DashboardProjectionShowsRecoveredFailedStateAfterStoreReload()
    {
        var harness = await CreateHarnessAsync();
        await WriteReadyArtifactsAsync(harness.Repository);
        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var reloadedService = new ExecutionSessionService(
            harness.ContextService,
            new FileSystemExecutionSessionStore(harness.StorePath),
            new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            new ExecutionMonitoringService(new FileSystemExecutionSessionStore(harness.StorePath)));
        await reloadedService.RecoverAsync();
        var artifactStore = new FileSystemArtifactStore();
        var projectionService = new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            reloadedService);

        var dashboard = await projectionService.GetDashboardAsync();
        var workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);

        var dashboardProjection = Assert.Single(dashboard);
        Assert.Equal(RepositoryExecutionState.Failed, dashboardProjection.ExecutionState);
        Assert.Null(dashboardProjection.ActiveExecutionSession);
        Assert.NotNull(dashboardProjection.ExecutionSummary);
        Assert.Equal(ExecutionSessionState.Failed, dashboardProjection.ExecutionSummary.State);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, dashboardProjection.ExecutionSummary.FailureReason);
        Assert.Equal(RepositoryExecutionState.Failed, workspace.ExecutionState);
        Assert.NotNull(workspace.ExecutionSummary);
        Assert.Equal(ExecutionSessionState.Failed, workspace.ExecutionSummary.State);
        Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, workspace.ExecutionSummary.FailureReason);
    }

    [Fact]
    public async Task ProviderStartFailureRemainsVisibleWhenRepositoryReturnsReady()
    {
        var provider = new FailingExecutionProvider(new ExecutionProviderException(
            "ProviderLaunchFailed",
            "Codex process failed to start."));
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });
        var active = await harness.SessionService.GetActiveSessionAsync(harness.Repository.Id);
        var repositorySummary = await harness.SessionService.GetRepositorySessionSummaryAsync(harness.Repository.Id);

        Assert.Equal(ExecutionSessionState.Failed, summary.State);
        Assert.Equal(RepositoryExecutionState.Ready, summary.RepositoryState);
        Assert.Null(active);
        Assert.NotNull(repositorySummary);
        Assert.Equal(ExecutionSessionState.Failed, repositorySummary.State);
        Assert.Equal(RepositoryExecutionState.Ready, repositorySummary.RepositoryState);
        Assert.Contains("ProviderLaunchFailed", repositorySummary.FailureReason);
    }

    [Fact]
    public async Task StartupRecoveryDoesNotMutateNonExecutingSessions()
    {
        var harness = await CreateHarnessAsync();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var failedSession = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = harness.Repository.Id,
            RepositoryPath = harness.Repository.Path,
            MilestonePath = ".agents/milestones/m2.md",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(1),
            LastActivityAt = startedAt.AddMinutes(1),
            State = ExecutionSessionState.Failed,
            RepositoryState = RepositoryExecutionState.Failed,
            ProviderName = "codex",
            ProviderExecutablePath = "C:\\tools\\codex.exe",
            ProviderProcessId = 7890,
            ProviderStartedAt = startedAt,
            PromptMetadata = new ExecutionPromptMetadata
            {
                RepositoryPath = harness.Repository.Path,
                MilestonePath = ".agents/milestones/m2.md",
                IncludedArtifactPaths = [".agents/plan.md"]
            },
            FailureReason = "Existing failure."
        };
        await harness.Store.SaveAsync([failedSession]);

        await harness.SessionService.RecoverAsync();
        var recoveredSession = (await harness.Store.LoadAsync()).Single();

        Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
        Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
        Assert.Equal("Existing failure.", recoveredSession.FailureReason);
        Assert.Equal(failedSession.CompletedAt, recoveredSession.CompletedAt);
        Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
        Assert.Equal(7890, recoveredSession.ProviderProcessId);
        Assert.NotNull(recoveredSession.PromptMetadata);
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
    public async Task ProviderReceivesExecutionPrompt()
    {
        var provider = new FakeExecutionProvider();
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        Assert.NotNull(provider.LastPrompt);
        Assert.Contains("Produce or update `.agents/handoffs/handoff.md`", provider.LastPrompt.Text);
        Assert.Equal(".agents/milestones/m2.md", provider.LastPrompt.Metadata.MilestonePath);
    }

    [Fact]
    public async Task ProviderLaunchMetadataPersistsAfterStoreReload()
    {
        var provider = new MetadataExecutionProvider();
        var harness = await CreateHarnessAsync(provider: provider);
        await WriteReadyArtifactsAsync(harness.Repository);

        var summary = await harness.SessionService.StartAsync(
            harness.Repository.Id,
            new ExecutionStartRequest { MilestonePath = ".agents/milestones/m2.md" });

        var sessions = await new FileSystemExecutionSessionStore(harness.StorePath).LoadAsync();
        var session = sessions.Single(session => session.Id == summary.SessionId);

        Assert.Equal("codex", session.ProviderName);
        Assert.Equal("C:\\tools\\codex.exe", session.ProviderExecutablePath);
        Assert.Equal(7890, session.ProviderProcessId);
        Assert.NotNull(session.ProviderStartedAt);
        Assert.NotNull(session.PromptMetadata);
        Assert.Equal(".agents/milestones/m2.md", session.PromptMetadata.MilestonePath);
        Assert.Equal([".agents/plan.md", ".agents/milestones/m2.md"], session.PromptMetadata.IncludedArtifactPaths);
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

    [Fact]
    public async Task AppStartupRunsExecutionRecovery()
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
            var session = new ExecutionSession
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                RepositoryPath = repository.Path,
                MilestonePath = ".agents/milestones/m2.md",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                State = ExecutionSessionState.Executing,
                RepositoryState = RepositoryExecutionState.Executing,
                ProviderName = "codex",
                ProviderExecutablePath = "C:\\tools\\codex.exe",
                ProviderProcessId = 7890,
                ProviderStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                PromptMetadata = new ExecutionPromptMetadata
                {
                    RepositoryPath = repository.Path,
                    MilestonePath = ".agents/milestones/m2.md",
                    IncludedArtifactPaths = [".agents/plan.md", ".agents/milestones/m2.md"]
                }
            };
            await new FileSystemExecutionSessionStore(storePath).SaveAsync([session]);

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
            var response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var recoveredSession = await response.Content.ReadFromJsonAsync<ExecutionSession>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            });
            Assert.NotNull(recoveredSession);
            Assert.Equal(ExecutionSessionState.Failed, recoveredSession.State);
            Assert.Equal(RepositoryExecutionState.Failed, recoveredSession.RepositoryState);
            Assert.Equal(ExecutionSessionService.OrphanedProviderFailureReason, recoveredSession.FailureReason);
            Assert.Equal("C:\\tools\\codex.exe", recoveredSession.ProviderExecutablePath);
            Assert.Equal(7890, recoveredSession.ProviderProcessId);
            Assert.NotNull(recoveredSession.PromptMetadata);
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
        IExecutionProvider? provider = null)
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
            provider ?? new FakeExecutionProvider(),
            new ExecutionPromptBuilder(),
            new ExecutionMonitoringService(store));

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

    private sealed class MetadataExecutionProvider : IExecutionProvider
    {
        public string Name => "codex";

        public Task<ExecutionProviderStartResult> StartAsync(
            ExecutionPrompt prompt,
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            return Task.FromResult(new ExecutionProviderStartResult
            {
                ProviderName = Name,
                ExecutablePath = "C:\\tools\\codex.exe",
                ProcessId = 7890,
                StartedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private sealed class FailingExecutionProvider(Exception exception) : IExecutionProvider
    {
        public string Name => "codex";

        public Task<ExecutionProviderStartResult> StartAsync(
            ExecutionPrompt prompt,
            ExecutionSession session,
            IExecutionProviderObserver observer)
        {
            throw exception;
        }
    }
}

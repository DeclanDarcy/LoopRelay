using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionMonitoringEndpointTests
{
    [Fact]
    public async Task StatusEndpointProjectsCompletedSessionWithRecentEventsAfterStoreReload()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithSessionAsync(storePath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("first");
        await observer.OnStdErrAsync("second");
        await observer.OnProviderExitedAsync(0);

        await using var app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadJsonAsync<ExecutionStatus>(response);
        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.Executing, status.RepositoryState);
        Assert.NotNull(status.CompletedAt);
        Assert.True(status.LastActivityAt >= session.LastActivityAt);
        Assert.Equal(
            [ExecutionEventType.StdOut, ExecutionEventType.StdErr, ExecutionEventType.ProviderExited],
            status.RecentEvents.Select(executionEvent => executionEvent.Type).ToArray());
        Assert.Equal([1, 2, 3], status.RecentEvents.Select(executionEvent => executionEvent.Sequence).ToArray());
    }

    [Fact]
    public async Task EventsEndpointReturnsRetainedEventsInChronologicalOrderAfterStoreReload()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(
            store,
            new ExecutionEventRetentionPolicy
            {
                MaximumEventCount = 2,
                MaximumEventBytes = 1024
            });
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("one");
        await observer.OnStdOutAsync("two");
        await observer.OnStdOutAsync("three");

        await using var app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await ReadJsonAsync<IReadOnlyList<ExecutionEvent>>(response);
        Assert.NotNull(events);
        Assert.Equal(["two", "three"], events.Select(executionEvent => executionEvent.Message).ToArray());
        Assert.Equal([2, 3], events.Select(executionEvent => executionEvent.Sequence).ToArray());
    }

    [Fact]
    public async Task StatusEndpointProjectsFailedSession()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithSessionAsync(storePath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderExitedAsync(2);

        await using var app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadJsonAsync<ExecutionStatus>(response);
        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Contains("2", status.FailureReason);
        var providerExitEvent = Assert.Single(status.RecentEvents);
        Assert.Equal(ExecutionEventType.ProviderExited, providerExitEvent.Type);
    }

    [Fact]
    public async Task StatusAndEventsEndpointsReturnNotFoundForUnknownSession()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");

        await using var app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var statusResponse = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{Guid.NewGuid()}/status");
        var eventsResponse = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, eventsResponse.StatusCode);
    }

    private static WebApplication CreateApp(string storePath)
    {
        return Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                services.AddSingleton<IExecutionProvider>(new FakeExecutionProvider());
                services.AddSingleton<IGitService>(new FakeGitService());
            });
    }

    private static async Task<FileSystemExecutionSessionStore> CreateStoreWithSessionAsync(
        string storePath,
        ExecutionSessionState state = ExecutionSessionState.Executing,
        RepositoryExecutionState repositoryState = RepositoryExecutionState.Executing)
    {
        var store = new FileSystemExecutionSessionStore(storePath);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await store.SaveAsync(
            [
                new ExecutionSession
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = Guid.NewGuid(),
                    RepositoryPath = CreateTemporaryDirectory(),
                    MilestonePath = ".agents/milestones/m3.md",
                    StartedAt = startedAt,
                    LastActivityAt = startedAt,
                    State = state,
                    RepositoryState = repositoryState,
                    ProviderName = "fake"
                }
            ]);
        return store;
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        });
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeGitService : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }
    }
}

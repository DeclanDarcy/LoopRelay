using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Execution;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Primitives;
using CommandCenter.Execution.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionMonitoringEndpointTests
{
    [Fact]
    public async Task StatusEndpointProjectsCompletedSessionWithRecentEventsAfterStoreReload()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(storePath);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("first");
        await observer.OnStdErrAsync("second");
        await observer.OnProviderExitedAsync(0);

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadJsonAsync<ExecutionStatus>(response);
        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.Executing, status.RepositoryState);
        Assert.NotNull(status.CompletedAt);
        Assert.Equal(status.CompletedAt.Value - session.StartedAt, status.Duration);
        Assert.True(status.LastActivityAt >= session.LastActivityAt);
        Assert.Equal(
            [ExecutionEventType.StdOut, ExecutionEventType.StdErr, ExecutionEventType.ProviderExited],
            status.RecentEvents.Select(executionEvent => executionEvent.Type).ToArray());
        Assert.Equal([1, 2, 3], status.RecentEvents.Select(executionEvent => executionEvent.Sequence).ToArray());
    }

    [Fact]
    public async Task EventsEndpointReturnsRetainedEventsInChronologicalOrderAfterStoreReload()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(
            store,
            new ExecutionEventRetentionPolicy
            {
                MaximumEventCount = 2,
                MaximumEventBytes = 1024
            });
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("one");
        await observer.OnStdOutAsync("two");
        await observer.OnStdOutAsync("three");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await ReadJsonAsync<IReadOnlyList<ExecutionEvent>>(response);
        Assert.NotNull(events);
        Assert.Equal(["two", "three"], events.Select(executionEvent => executionEvent.Message).ToArray());
        Assert.Equal([2, 3], events.Select(executionEvent => executionEvent.Sequence).ToArray());
    }

    [Fact]
    public async Task StatusEndpointProjectsFailedSession()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderExitedAsync(2);

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadJsonAsync<ExecutionStatus>(response);
        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Contains("2", status.FailureReason);
        ExecutionEvent providerExitEvent = Assert.Single(status.RecentEvents);
        Assert.Equal(ExecutionEventType.ProviderExited, providerExitEvent.Type);
    }

    [Fact]
    public async Task StatusEndpointProjectsCancelledSession()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(storePath);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderCancelledAsync("Provider cancellation was requested.");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{session.Id}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadJsonAsync<ExecutionStatus>(response);
        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Cancelled, status.State);
        Assert.Equal(RepositoryExecutionState.Cancelled, status.RepositoryState);
        ExecutionEvent cancellationEvent = Assert.Single(status.RecentEvents);
        Assert.Equal(ExecutionEventType.Cancellation, cancellationEvent.Type);
    }

    [Fact]
    public async Task EventsStreamEndpointStreamsCancellationEvent()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(storePath);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        await monitoringService.CreateProviderObserver(session.Id).OnProviderCancelledAsync("Provider cancellation was requested.");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        using var reader = new StreamReader(stream);

        ExecutionEvent cancellationEvent = Assert.Single(await ReadSseEventsAsync(reader, 1, cancellationTokenSource.Token));

        Assert.Equal(ExecutionEventType.Cancellation, cancellationEvent.Type);
        Assert.Equal("Provider cancellation was requested.", cancellationEvent.Message);
    }

    [Fact]
    public async Task StatusAndEventsEndpointsReturnNotFoundForUnknownSession()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage statusResponse = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{Guid.NewGuid()}/status");
        HttpResponseMessage eventsResponse = await client.GetAsync(app.Urls.Single() + $"/api/execution-sessions/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, eventsResponse.StatusCode);
    }

    [Fact]
    public async Task EventsStreamEndpointStreamsRetainedEventsInChronologicalOrder()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("first");
        await observer.OnStdErrAsync("second");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        using var reader = new StreamReader(stream);

        IReadOnlyList<ExecutionEvent> events = await ReadSseEventsAsync(reader, 2, cancellationTokenSource.Token);

        Assert.Equal(["first", "second"], events.Select(executionEvent => executionEvent.Message).ToArray());
        Assert.Equal([1, 2], events.Select(executionEvent => executionEvent.Sequence).ToArray());
        Assert.Equal([ExecutionEventType.StdOut, ExecutionEventType.StdErr], events.Select(executionEvent => executionEvent.Type).ToArray());
    }

    [Fact]
    public async Task EventsStreamEndpointStreamsRetainedAndLiveEvents()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var seedMonitoringService = new ExecutionMonitoringService(store);
        await seedMonitoringService.CreateProviderObserver(session.Id).OnStdOutAsync("retained");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        using var reader = new StreamReader(stream);

        ExecutionEvent retainedEvent = Assert.Single(await ReadSseEventsAsync(reader, 1, cancellationTokenSource.Token));
        IExecutionProviderObserver liveObserver = app.Services.GetRequiredService<IExecutionMonitoringService>().CreateProviderObserver(session.Id);
        await liveObserver.OnStdErrAsync("live");
        ExecutionEvent liveEvent = Assert.Single(await ReadSseEventsAsync(reader, 1, cancellationTokenSource.Token));

        Assert.Equal("retained", retainedEvent.Message);
        Assert.Equal("live", liveEvent.Message);
        Assert.Equal(2, liveEvent.Sequence);
    }

    [Fact]
    public async Task EventsStreamEndpointAllowsDisconnectWithoutBreakingFutureEvents()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var seedMonitoringService = new ExecutionMonitoringService(store);
        await seedMonitoringService.CreateProviderObserver(session.Id).OnStdOutAsync("retained");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        using (var client = new HttpClient())
        using (HttpResponseMessage response = await client.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token))
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
            using var reader = new StreamReader(stream);
            Assert.Single(await ReadSseEventsAsync(reader, 1, cancellationTokenSource.Token));
        }

        IExecutionProviderObserver observer = app.Services.GetRequiredService<IExecutionMonitoringService>().CreateProviderObserver(session.Id);
        await observer.OnStdOutAsync("after disconnect");

        IReadOnlyList<ExecutionEvent> events = await app.Services.GetRequiredService<IExecutionMonitoringService>().GetEventsAsync(session.Id);
        Assert.Equal(["retained", "after disconnect"], events.Select(executionEvent => executionEvent.Message).ToArray());
    }

    [Fact]
    public async Task EventsStreamEndpointSupportsMultipleSimultaneousConsumers()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(
            storePath,
            ExecutionSessionState.Failed,
            RepositoryExecutionState.Failed);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var seedMonitoringService = new ExecutionMonitoringService(store);
        await seedMonitoringService.CreateProviderObserver(session.Id).OnStdOutAsync("retained");

        await using WebApplication app = CreateApp(storePath);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var firstClient = new HttpClient();
        using var secondClient = new HttpClient();
        using HttpResponseMessage firstResponse = await firstClient.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token);
        using HttpResponseMessage secondResponse = await secondClient.GetAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/events/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationTokenSource.Token);
        await using Stream firstStream = await firstResponse.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        await using Stream secondStream = await secondResponse.Content.ReadAsStreamAsync(cancellationTokenSource.Token);
        using var firstReader = new StreamReader(firstStream);
        using var secondReader = new StreamReader(secondStream);

        Assert.Single(await ReadSseEventsAsync(firstReader, 1, cancellationTokenSource.Token));
        Assert.Single(await ReadSseEventsAsync(secondReader, 1, cancellationTokenSource.Token));

        IExecutionProviderObserver observer = app.Services.GetRequiredService<IExecutionMonitoringService>().CreateProviderObserver(session.Id);
        await observer.OnStdOutAsync("broadcast");

        ExecutionEvent firstLiveEvent = Assert.Single(await ReadSseEventsAsync(firstReader, 1, cancellationTokenSource.Token));
        ExecutionEvent secondLiveEvent = Assert.Single(await ReadSseEventsAsync(secondReader, 1, cancellationTokenSource.Token));

        Assert.Equal("broadcast", firstLiveEvent.Message);
        Assert.Equal("broadcast", secondLiveEvent.Message);
        Assert.Equal(2, firstLiveEvent.Sequence);
        Assert.Equal(2, secondLiveEvent.Sequence);
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
        DateTimeOffset startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
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

    private static async Task<IReadOnlyList<ExecutionEvent>> ReadSseEventsAsync(
        StreamReader reader,
        int count,
        CancellationToken cancellationToken)
    {
        var events = new List<ExecutionEvent>();
        string data = string.Empty;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        while (events.Count < count)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    var executionEvent = JsonSerializer.Deserialize<ExecutionEvent>(data, jsonOptions);
                    Assert.NotNull(executionEvent);
                    events.Add(executionEvent);
                    data = string.Empty;
                }

                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line["data: ".Length..];
            }
        }

        return events;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            throw new NotSupportedException();
        }

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            throw new NotSupportedException();
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            throw new NotSupportedException();
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            throw new NotSupportedException();
        }
    }
}

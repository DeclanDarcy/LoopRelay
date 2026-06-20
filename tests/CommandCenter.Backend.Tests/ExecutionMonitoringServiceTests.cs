using CommandCenter.Backend.Execution;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionMonitoringServiceTests
{
    [Fact]
    public async Task ProviderOutputEventsAreRetainedInChronologicalOrder()
    {
        var store = await CreateStoreWithSessionAsync();
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("first");
        await observer.OnStdErrAsync("second");

        var events = await monitoringService.GetEventsAsync(session.Id);

        Assert.Collection(
            events,
            executionEvent =>
            {
                Assert.Equal(1, executionEvent.Sequence);
                Assert.Equal(ExecutionEventType.StdOut, executionEvent.Type);
                Assert.Equal("first", executionEvent.Message);
            },
            executionEvent =>
            {
                Assert.Equal(2, executionEvent.Sequence);
                Assert.Equal(ExecutionEventType.StdErr, executionEvent.Type);
                Assert.Equal("second", executionEvent.Message);
            });
    }

    [Fact]
    public async Task ProviderOutputUpdatesLastActivityAt()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var store = await CreateStoreWithSessionAsync(startedAt);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("activity");
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.True(status.LastActivityAt > startedAt);
    }

    [Fact]
    public async Task NonZeroProviderExitRecordsFailureEventAndState()
    {
        var store = await CreateStoreWithSessionAsync();
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderExitedAsync(2);
        var status = await monitoringService.GetStatusAsync(session.Id);
        var events = await monitoringService.GetEventsAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Contains("2", status.FailureReason);
        var providerExitEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.ProviderExited, providerExitEvent.Type);
        Assert.Contains("2", providerExitEvent.Message);
    }

    [Fact]
    public async Task ProviderCancellationRecordsCancellationEventAndState()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var store = await CreateStoreWithSessionAsync(startedAt);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderCancelledAsync("Provider cancellation was requested.");
        var status = await monitoringService.GetStatusAsync(session.Id);
        var events = await monitoringService.GetEventsAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Cancelled, status.State);
        Assert.Equal(RepositoryExecutionState.Cancelled, status.RepositoryState);
        Assert.NotNull(status.CompletedAt);
        Assert.Equal(status.CompletedAt.Value - startedAt, status.Duration);
        Assert.True(status.LastActivityAt > startedAt);
        var cancellationEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.Cancellation, cancellationEvent.Type);
        Assert.Equal("Provider cancellation was requested.", cancellationEvent.Message);
    }

    [Fact]
    public async Task CancellationStatusSurvivesStoreReload()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithSessionAsync(storePath: storePath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderCancelledAsync("cancelled");
        var reloadedMonitoringService = new ExecutionMonitoringService(new FileSystemExecutionSessionStore(storePath));
        var status = await reloadedMonitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Cancelled, status.State);
        Assert.Equal(RepositoryExecutionState.Cancelled, status.RepositoryState);
        Assert.NotNull(status.Duration);
        Assert.Equal(ExecutionEventType.Cancellation, Assert.Single(status.RecentEvents).Type);
    }

    [Fact]
    public async Task EventHistoryIsBoundedByRetentionPolicy()
    {
        var store = await CreateStoreWithSessionAsync();
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(
            store,
            new ExecutionEventRetentionPolicy
            {
                MaximumEventCount = 3,
                MaximumEventBytes = 1024
            });
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("one");
        await observer.OnStdOutAsync("two");
        await observer.OnStdOutAsync("three");
        await observer.OnStdOutAsync("four");

        var events = await monitoringService.GetEventsAsync(session.Id);

        Assert.Equal(3, events.Count);
        Assert.Equal(["two", "three", "four"], events.Select(executionEvent => executionEvent.Message).ToArray());
    }

    [Fact]
    public async Task RetainedEventsSurviveStoreReload()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithSessionAsync(storePath: storePath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        var observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("persisted");
        var reloadedMonitoringService = new ExecutionMonitoringService(new FileSystemExecutionSessionStore(storePath));
        var events = await reloadedMonitoringService.GetEventsAsync(session.Id);

        var executionEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.StdOut, executionEvent.Type);
        Assert.Equal("persisted", executionEvent.Message);
    }

    private static async Task<FileSystemExecutionSessionStore> CreateStoreWithSessionAsync(
        DateTimeOffset? startedAt = null,
        string? storePath = null)
    {
        var store = new FileSystemExecutionSessionStore(
            storePath ?? Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json"));
        var sessionStartedAt = startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.SaveAsync(
            [
                new ExecutionSession
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = Guid.NewGuid(),
                    RepositoryPath = CreateTemporaryDirectory(),
                    MilestonePath = ".agents/milestones/m3.md",
                    StartedAt = sessionStartedAt,
                    LastActivityAt = sessionStartedAt,
                    State = ExecutionSessionState.Executing,
                    RepositoryState = RepositoryExecutionState.Executing,
                    ProviderName = "fake"
                }
            ]);
        return store;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

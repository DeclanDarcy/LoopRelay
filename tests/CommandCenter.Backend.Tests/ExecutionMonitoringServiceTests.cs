using CommandCenter.Execution;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Execution.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionMonitoringServiceTests
{
    [Fact]
    public async Task ProviderOutputEventsAreRetainedInChronologicalOrder()
    {
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync();
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("first");
        await observer.OnStdErrAsync("second");

        IReadOnlyList<ExecutionEvent> events = await monitoringService.GetEventsAsync(session.Id);

        Assert.Collection(
            events,
            executionEvent =>
            {
                Assert.Equal(1, executionEvent.Sequence);
                Assert.Equal(ExecutionEventType.StdOut, executionEvent.Type);
                Assert.Equal("Provider", executionEvent.Category);
                Assert.Equal("Provider emitted standard output.", executionEvent.Consequence);
                Assert.Equal("first", executionEvent.Message);
            },
            executionEvent =>
            {
                Assert.Equal(2, executionEvent.Sequence);
                Assert.Equal(ExecutionEventType.StdErr, executionEvent.Type);
                Assert.Equal("Provider", executionEvent.Category);
                Assert.Equal("Provider emitted standard error output.", executionEvent.Consequence);
                Assert.Equal("second", executionEvent.Message);
            });
    }

    [Fact]
    public async Task ProviderOutputUpdatesLastActivityAt()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(startedAt);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("activity");
        ExecutionStatus? status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.True(status.LastActivityAt > startedAt);
    }

    [Fact]
    public async Task NonZeroProviderExitRecordsFailureEventAndState()
    {
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync();
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderExitedAsync(2);
        ExecutionStatus? status = await monitoringService.GetStatusAsync(session.Id);
        IReadOnlyList<ExecutionEvent> events = await monitoringService.GetEventsAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Contains("2", status.FailureReason);
        ExecutionEvent providerExitEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.ProviderExited, providerExitEvent.Type);
        Assert.Equal("Provider", providerExitEvent.Category);
        Assert.Equal(
            "Provider exited; session state reflects the observed exit result.",
            providerExitEvent.Consequence);
        Assert.Contains("2", providerExitEvent.Message);
    }

    [Fact]
    public async Task EventsWithoutPersistedSemanticsAreProjectedWithCategoriesAndConsequences()
    {
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync();
        ExecutionSession session = (await store.LoadAsync()).Single();
        await store.SaveAsync(
            [
                CopySessionWithEvents(
                    session,
                    [
                        new ExecutionEvent
                        {
                            Sequence = 1,
                            Timestamp = DateTimeOffset.UtcNow,
                            Type = ExecutionEventType.HandoffValidated,
                            Message = "Current handoff validated for review."
                        }
                    ])
            ]);
        var monitoringService = new ExecutionMonitoringService(store);

        ExecutionEvent executionEvent = Assert.Single(await monitoringService.GetEventsAsync(session.Id));

        Assert.Equal("Handoff", executionEvent.Category);
        Assert.Equal(
            "Handoff passed validation and the repository is awaiting acceptance.",
            executionEvent.Consequence);
    }

    [Fact]
    public async Task ProviderCancellationRecordsCancellationEventAndState()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(startedAt);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnProviderCancelledAsync("Provider cancellation was requested.");
        ExecutionStatus? status = await monitoringService.GetStatusAsync(session.Id);
        IReadOnlyList<ExecutionEvent> events = await monitoringService.GetEventsAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Cancelled, status.State);
        Assert.Equal(RepositoryExecutionState.Cancelled, status.RepositoryState);
        Assert.NotNull(status.CompletedAt);
        Assert.Equal(status.CompletedAt.Value - startedAt, status.Duration);
        Assert.True(status.LastActivityAt > startedAt);
        ExecutionEvent cancellationEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.Cancellation, cancellationEvent.Type);
        Assert.Equal("Provider cancellation was requested.", cancellationEvent.Message);
    }

    [Fact]
    public async Task CancellationStatusSurvivesStoreReload()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(storePath: storePath);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderCancelledAsync("cancelled");
        var reloadedMonitoringService = new ExecutionMonitoringService(new FileSystemExecutionSessionStore(storePath));
        ExecutionStatus? status = await reloadedMonitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Cancelled, status.State);
        Assert.Equal(RepositoryExecutionState.Cancelled, status.RepositoryState);
        Assert.NotNull(status.Duration);
        Assert.Equal(ExecutionEventType.Cancellation, Assert.Single(status.RecentEvents).Type);
    }

    [Fact]
    public async Task EventHistoryIsBoundedByRetentionPolicy()
    {
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync();
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(
            store,
            new ExecutionEventRetentionPolicy
            {
                MaximumEventCount = 3,
                MaximumEventBytes = 1024
            });
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("one");
        await observer.OnStdOutAsync("two");
        await observer.OnStdOutAsync("three");
        await observer.OnStdOutAsync("four");

        IReadOnlyList<ExecutionEvent> events = await monitoringService.GetEventsAsync(session.Id);

        Assert.Equal(3, events.Count);
        Assert.Equal(["two", "three", "four"], events.Select(executionEvent => executionEvent.Message).ToArray());
    }

    [Fact]
    public async Task RetainedEventsSurviveStoreReload()
    {
        string storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        FileSystemExecutionSessionStore store = await CreateStoreWithSessionAsync(storePath: storePath);
        ExecutionSession session = (await store.LoadAsync()).Single();
        var monitoringService = new ExecutionMonitoringService(store);
        IExecutionProviderObserver observer = monitoringService.CreateProviderObserver(session.Id);

        await observer.OnStdOutAsync("persisted");
        var reloadedMonitoringService = new ExecutionMonitoringService(new FileSystemExecutionSessionStore(storePath));
        IReadOnlyList<ExecutionEvent> events = await reloadedMonitoringService.GetEventsAsync(session.Id);

        ExecutionEvent executionEvent = Assert.Single(events);
        Assert.Equal(ExecutionEventType.StdOut, executionEvent.Type);
        Assert.Equal("persisted", executionEvent.Message);
    }

    private static async Task<FileSystemExecutionSessionStore> CreateStoreWithSessionAsync(
        DateTimeOffset? startedAt = null,
        string? storePath = null)
    {
        var store = new FileSystemExecutionSessionStore(
            storePath ?? Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json"));
        DateTimeOffset sessionStartedAt = startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1);
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

    private static ExecutionSession CopySessionWithEvents(
        ExecutionSession session,
        IReadOnlyList<ExecutionEvent> events)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            MilestonePath = session.MilestonePath,
            StartedAt = session.StartedAt,
            LastActivityAt = session.LastActivityAt,
            State = session.State,
            RepositoryState = session.RepositoryState,
            ProviderName = session.ProviderName,
            Events = events
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

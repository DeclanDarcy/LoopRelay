using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Execution;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionHandoffServiceTests
{
    [Fact]
    public async Task ProviderCompletionWithCurrentHandoffTransitionsToAwaitingAcceptance()
    {
        var repositoryPath = CreateTemporaryDirectory();
        await WriteAsync(repositoryPath, ".agents/handoffs/handoff.md", "generated handoff");
        var store = await CreateStoreWithExecutingSessionAsync(repositoryPath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, status.RepositoryState);
        Assert.Equal(HandoffService.CurrentHandoffPath, status.HandoffPath);
        Assert.NotNull(status.CompletedAt);
    }

    [Fact]
    public async Task ProviderCompletionWithoutCurrentHandoffTransitionsToFailed()
    {
        var repositoryPath = CreateTemporaryDirectory();
        var store = await CreateStoreWithExecutingSessionAsync(repositoryPath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Equal(HandoffService.MissingCurrentHandoffFailureReason, status.FailureReason);
        Assert.Null(status.HandoffPath);
    }

    [Fact]
    public async Task NonZeroProviderExitDoesNotValidateHandoff()
    {
        var repositoryPath = CreateTemporaryDirectory();
        var store = await CreateStoreWithExecutingSessionAsync(repositoryPath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(2);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Contains("2", status.FailureReason);
        Assert.NotEqual(HandoffService.MissingCurrentHandoffFailureReason, status.FailureReason);
    }

    [Fact]
    public async Task AwaitingAcceptanceStateSurvivesStoreReload()
    {
        var repositoryPath = CreateTemporaryDirectory();
        await WriteAsync(repositoryPath, ".agents/handoffs/handoff.md", "generated handoff");
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var store = await CreateStoreWithExecutingSessionAsync(repositoryPath, storePath);
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var reloadedMonitoringService = new ExecutionMonitoringService(new FileSystemExecutionSessionStore(storePath));
        var status = await reloadedMonitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, status.RepositoryState);
        Assert.Equal(HandoffService.CurrentHandoffPath, status.HandoffPath);
    }

    private static ExecutionMonitoringService CreateMonitoringService(FileSystemExecutionSessionStore store)
    {
        return new ExecutionMonitoringService(
            store,
            new HandoffService(store, new FileSystemArtifactStore()));
    }

    private static async Task<FileSystemExecutionSessionStore> CreateStoreWithExecutingSessionAsync(
        string repositoryPath,
        string? storePath = null)
    {
        var store = new FileSystemExecutionSessionStore(
            storePath ?? Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json"));
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.SaveAsync(
            [
                new ExecutionSession
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = Guid.NewGuid(),
                    RepositoryPath = repositoryPath,
                    MilestonePath = ".agents/milestones/m4.md",
                    StartedAt = startedAt,
                    LastActivityAt = startedAt,
                    State = ExecutionSessionState.Executing,
                    RepositoryState = RepositoryExecutionState.Executing,
                    ProviderName = "fake"
                }
            ]);
        return store;
    }

    private static async Task WriteAsync(string repositoryPath, string relativePath, string content)
    {
        var path = Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

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

    [Fact]
    public async Task PreviousHandoffSnapshotIsArchivedToNextHistoricalSequence()
    {
        var repositoryPath = CreateTemporaryDirectory();
        await WriteAsync(repositoryPath, ".agents/handoffs/handoff.md", "generated handoff");
        await WriteAsync(repositoryPath, ".agents/handoffs/handoff.0004.md", "historical handoff");
        var store = await CreateStoreWithExecutingSessionAsync(
            repositoryPath,
            previousHandoffContent: "previous handoff");
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, status.RepositoryState);
        Assert.Equal("previous handoff", await ReadAsync(repositoryPath, ".agents/handoffs/handoff.0005.md"));
        Assert.Equal("generated handoff", await ReadAsync(repositoryPath, ".agents/handoffs/handoff.md"));
    }

    [Fact]
    public async Task PreviousHandoffSnapshotIsNotArchivedWhenCurrentHandoffIsUnchanged()
    {
        var repositoryPath = CreateTemporaryDirectory();
        await WriteAsync(repositoryPath, ".agents/handoffs/handoff.md", "same handoff");
        var store = await CreateStoreWithExecutingSessionAsync(
            repositoryPath,
            previousHandoffContent: "same handoff");
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Completed, status.State);
        Assert.Equal(RepositoryExecutionState.AwaitingAcceptance, status.RepositoryState);
        Assert.False(File.Exists(Path.Combine(repositoryPath, ".agents", "handoffs", "handoff.0001.md")));
    }

    [Fact]
    public async Task NoHistoricalHandoffIsCreatedWhenNoPreviousSnapshotWasCaptured()
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
        Assert.False(File.Exists(Path.Combine(repositoryPath, ".agents", "handoffs", "handoff.0001.md")));
    }

    [Fact]
    public async Task ArchiveFailureTransitionsToFailedAndKeepsCurrentHandoff()
    {
        var repositoryPath = CreateTemporaryDirectory();
        var artifactStore = new ArchiveWriteFailureArtifactStore(new FileSystemArtifactStore());
        await artifactStore.WriteAsync(
            Path.Combine(repositoryPath, ".agents", "handoffs", "handoff.md"),
            "generated handoff");
        var store = await CreateStoreWithExecutingSessionAsync(
            repositoryPath,
            previousHandoffContent: "previous handoff");
        var session = (await store.LoadAsync()).Single();
        var monitoringService = CreateMonitoringService(store, artifactStore);

        await monitoringService.CreateProviderObserver(session.Id).OnProviderExitedAsync(0);
        var status = await monitoringService.GetStatusAsync(session.Id);

        Assert.NotNull(status);
        Assert.Equal(ExecutionSessionState.Failed, status.State);
        Assert.Equal(RepositoryExecutionState.Failed, status.RepositoryState);
        Assert.Equal(HandoffService.CurrentHandoffPath, status.HandoffPath);
        Assert.Equal(HandoffService.ArchivePreviousHandoffFailureReason, status.FailureReason);
        Assert.Equal("generated handoff", await ReadAsync(repositoryPath, ".agents/handoffs/handoff.md"));
    }

    private static ExecutionMonitoringService CreateMonitoringService(
        FileSystemExecutionSessionStore store,
        IArtifactStore? artifactStore = null)
    {
        return new ExecutionMonitoringService(
            store,
            new HandoffService(store, artifactStore ?? new FileSystemArtifactStore()));
    }

    private static async Task<FileSystemExecutionSessionStore> CreateStoreWithExecutingSessionAsync(
        string repositoryPath,
        string? storePath = null,
        string? previousHandoffContent = null)
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
                    ProviderName = "fake",
                    PreviousHandoffContent = previousHandoffContent,
                    PreviousHandoffCapturedAt = previousHandoffContent is null
                        ? null
                        : startedAt.AddSeconds(-1)
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

    private static async Task<string> ReadAsync(string repositoryPath, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class ArchiveWriteFailureArtifactStore(IArtifactStore innerStore) : IArtifactStore
    {
        public Task<bool> ExistsAsync(string path)
        {
            return innerStore.ExistsAsync(path);
        }

        public Task<string?> ReadAsync(string path)
        {
            return innerStore.ReadAsync(path);
        }

        public Task WriteAsync(string path, string content)
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Length == "handoff.0001.md".Length &&
                fileName.StartsWith("handoff.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Archive write failed.");
            }

            return innerStore.WriteAsync(path, content);
        }

        public Task DeleteAsync(string path)
        {
            return innerStore.DeleteAsync(path);
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
        {
            return innerStore.ListAsync(path, searchPattern);
        }
    }
}

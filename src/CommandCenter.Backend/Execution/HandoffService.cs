using CommandCenter.Backend.Artifacts;

namespace CommandCenter.Backend.Execution;

public sealed class HandoffService(
    IExecutionSessionStore sessionStore,
    IArtifactStore artifactStore) : IHandoffService
{
    public const string CurrentHandoffPath = ".agents/handoffs/handoff.md";

    public const string MissingCurrentHandoffFailureReason =
        "Execution completed but no current handoff was found.";

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task ProcessProviderCompletionAsync(Guid sessionId)
    {
        await gate.WaitAsync();
        try
        {
            var sessions = (await sessionStore.LoadAsync()).ToList();
            var index = sessions.FindIndex(session => session.Id == sessionId);
            if (index < 0)
            {
                return;
            }

            var session = sessions[index];
            if (session.State != ExecutionSessionState.Completed ||
                session.RepositoryState != RepositoryExecutionState.Executing)
            {
                return;
            }

            var processedAt = DateTimeOffset.UtcNow;
            var handoffPath = Path.Combine(
                session.RepositoryPath,
                CurrentHandoffPath.Replace('/', Path.DirectorySeparatorChar));
            var handoffExists = await artifactStore.ExistsAsync(handoffPath);

            sessions[index] = CopySession(
                session,
                state: handoffExists ? ExecutionSessionState.Completed : ExecutionSessionState.Failed,
                repositoryState: handoffExists
                    ? RepositoryExecutionState.AwaitingAcceptance
                    : RepositoryExecutionState.Failed,
                completedAt: session.CompletedAt ?? processedAt,
                lastActivityAt: processedAt,
                handoffPath: handoffExists ? CurrentHandoffPath : session.HandoffPath,
                failureReason: handoffExists ? session.FailureReason : MissingCurrentHandoffFailureReason);

            await sessionStore.SaveAsync(sessions);
        }
        finally
        {
            gate.Release();
        }
    }

    private static ExecutionSession CopySession(
        ExecutionSession session,
        ExecutionSessionState state,
        RepositoryExecutionState repositoryState,
        DateTimeOffset completedAt,
        DateTimeOffset lastActivityAt,
        string? handoffPath,
        string? failureReason)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            MilestonePath = session.MilestonePath,
            StartedAt = session.StartedAt,
            CompletedAt = completedAt,
            LastActivityAt = lastActivityAt,
            State = state,
            RepositoryState = repositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            RepositorySnapshot = session.RepositorySnapshot,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = handoffPath,
            FailureReason = failureReason,
            Events = session.Events
        };
    }
}

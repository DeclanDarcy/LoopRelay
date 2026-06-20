using CommandCenter.Backend.Artifacts;

namespace CommandCenter.Backend.Execution;

public sealed class HandoffService(
    IExecutionSessionStore sessionStore,
    IArtifactStore artifactStore) : IHandoffService
{
    public const string CurrentHandoffPath = ".agents/handoffs/handoff.md";

    public const string MissingCurrentHandoffFailureReason =
        "Execution completed but no current handoff was found.";

    public const string ArchivePreviousHandoffFailureReason =
        "Execution completed but the previous handoff could not be archived.";

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
            var currentHandoff = await artifactStore.ReadAsync(handoffPath);
            if (currentHandoff is null)
            {
                sessions[index] = CopySession(
                    session,
                    state: ExecutionSessionState.Failed,
                    repositoryState: RepositoryExecutionState.Failed,
                    completedAt: session.CompletedAt ?? processedAt,
                    lastActivityAt: processedAt,
                    handoffPath: session.HandoffPath,
                    failureReason: MissingCurrentHandoffFailureReason);

                await sessionStore.SaveAsync(sessions);
                return;
            }

            var archiveFailure = false;
            if (ShouldArchivePreviousHandoff(session, currentHandoff))
            {
                try
                {
                    await ArchivePreviousHandoffAsync(session);
                }
                catch
                {
                    archiveFailure = true;
                }
            }

            sessions[index] = CopySession(
                session,
                state: archiveFailure ? ExecutionSessionState.Failed : ExecutionSessionState.Completed,
                repositoryState: archiveFailure
                    ? RepositoryExecutionState.Failed
                    : RepositoryExecutionState.AwaitingAcceptance,
                completedAt: session.CompletedAt ?? processedAt,
                lastActivityAt: processedAt,
                handoffPath: CurrentHandoffPath,
                failureReason: archiveFailure
                    ? ArchivePreviousHandoffFailureReason
                    : session.FailureReason);

            await sessionStore.SaveAsync(sessions);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ArchivePreviousHandoffAsync(ExecutionSession session)
    {
        var directory = Path.Combine(session.RepositoryPath, ".agents", "handoffs");
        var files = await artifactStore.ListAsync(directory, "*.md");
        var nextSequence = files
            .Select(file => TryParseHistoricalHandoffSequence(Path.GetFileName(file)))
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var targetRelativePath = $".agents/handoffs/handoff.{nextSequence:0000}.md";
        var targetPath = Path.Combine(
            session.RepositoryPath,
            targetRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (await artifactStore.ExistsAsync(targetPath))
        {
            throw new IOException($"Historical handoff already exists: {targetRelativePath}");
        }

        await artifactStore.WriteAsync(targetPath, session.PreviousHandoffContent!);
    }

    private static bool ShouldArchivePreviousHandoff(ExecutionSession session, string currentHandoff)
    {
        return session.PreviousHandoffContent is not null &&
            !string.Equals(session.PreviousHandoffContent, currentHandoff, StringComparison.Ordinal);
    }

    private static int? TryParseHistoricalHandoffSequence(string fileName)
    {
        const string prefix = "handoff.";
        const string suffix = ".md";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            fileName.Length != prefix.Length + 4 + suffix.Length)
        {
            return null;
        }

        var sequenceText = fileName[prefix.Length..^suffix.Length];
        if (sequenceText.Length != 4 ||
            !sequenceText.All(char.IsDigit) ||
            !int.TryParse(sequenceText, out var sequence) ||
            sequence <= 0)
        {
            return null;
        }

        return sequence;
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

using CommandCenter.Core.Artifacts;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Services;

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
            List<ExecutionSession> sessions = (await sessionStore.LoadAsync()).ToList();
            int index = sessions.FindIndex(session => session.Id == sessionId);
            if (index < 0)
            {
                return;
            }

            ExecutionSession session = sessions[index];
            if (session.State != ExecutionSessionState.Completed ||
                session.RepositoryState != RepositoryExecutionState.Executing)
            {
                return;
            }

            DateTimeOffset processedAt = DateTimeOffset.UtcNow;
            string handoffPath = Path.Combine(
                session.RepositoryPath,
                CurrentHandoffPath.Replace('/', Path.DirectorySeparatorChar));
            string? currentHandoff = await artifactStore.ReadAsync(handoffPath);
            if (currentHandoff is null)
            {
                var processing = new ExecutionHandoffProcessing
                {
                    HandoffProduced = false,
                    HandoffMissing = true,
                    HandoffArchived = false,
                    ArchiveFailed = false,
                    HandoffValidated = false,
                    ValidationFailure = MissingCurrentHandoffFailureReason,
                    ResultingSessionState = ExecutionSessionState.Failed,
                    ResultingRepositoryState = RepositoryExecutionState.Failed,
                    ProcessedAt = processedAt,
                    ProviderFailureDistinctFromHandoffFailure = false,
                    HandoffFailureReason = MissingCurrentHandoffFailureReason
                };
                sessions[index] = CopySession(
                    session,
                    state: ExecutionSessionState.Failed,
                    repositoryState: RepositoryExecutionState.Failed,
                    completedAt: session.CompletedAt ?? processedAt,
                    lastActivityAt: processedAt,
                    handoffPath: session.HandoffPath,
                    handoffProcessing: processing,
                    failureReason: MissingCurrentHandoffFailureReason);

                await sessionStore.SaveAsync(sessions);
                return;
            }

            bool archiveFailure = false;
            string? archivePath = null;
            int? archiveSequence = null;
            if (ShouldArchivePreviousHandoff(session, currentHandoff))
            {
                (archivePath, archiveSequence) = await GetNextHistoricalHandoffTargetAsync(session);
                try
                {
                    await ArchivePreviousHandoffAsync(session, archivePath);
                }
                catch
                {
                    archiveFailure = true;
                }
            }

            ExecutionSessionState resultingSessionState = archiveFailure
                ? ExecutionSessionState.Failed
                : ExecutionSessionState.Completed;
            RepositoryExecutionState resultingRepositoryState = archiveFailure
                ? RepositoryExecutionState.Failed
                : RepositoryExecutionState.AwaitingAcceptance;
            var successfulProcessing = new ExecutionHandoffProcessing
            {
                HandoffProduced = true,
                HandoffMissing = false,
                HandoffArchived = archivePath is not null && !archiveFailure,
                ArchivePath = archivePath,
                ArchiveSequence = archiveSequence,
                ArchiveFailed = archiveFailure,
                HandoffValidated = !archiveFailure,
                ValidationFailure = archiveFailure ? ArchivePreviousHandoffFailureReason : null,
                ResultingSessionState = resultingSessionState,
                ResultingRepositoryState = resultingRepositoryState,
                ProcessedAt = processedAt,
                ProviderFailureDistinctFromHandoffFailure = false,
                HandoffFailureReason = archiveFailure ? ArchivePreviousHandoffFailureReason : null
            };
            sessions[index] = CopySession(
                session,
                state: resultingSessionState,
                repositoryState: resultingRepositoryState,
                completedAt: session.CompletedAt ?? processedAt,
                lastActivityAt: processedAt,
                handoffPath: CurrentHandoffPath,
                handoffProcessing: successfulProcessing,
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

    private async Task<(string RelativePath, int Sequence)> GetNextHistoricalHandoffTargetAsync(ExecutionSession session)
    {
        string directory = Path.Combine(session.RepositoryPath, ".agents", "handoffs");
        IReadOnlyList<string> files = await artifactStore.ListAsync(directory, "*.md");
        int nextSequence = files
            .Select(file => TryParseHistoricalHandoffSequence(Path.GetFileName(file)))
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
        string targetRelativePath = $".agents/handoffs/handoff.{nextSequence:0000}.md";
        string targetPath = Path.Combine(
            session.RepositoryPath,
            targetRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (await artifactStore.ExistsAsync(targetPath))
        {
            throw new IOException($"Historical handoff already exists: {targetRelativePath}");
        }

        return (targetRelativePath, nextSequence);
    }

    private async Task ArchivePreviousHandoffAsync(ExecutionSession session, string targetRelativePath)
    {
        string targetPath = Path.Combine(
            session.RepositoryPath,
            targetRelativePath.Replace('/', Path.DirectorySeparatorChar));

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

        string sequenceText = fileName[prefix.Length..^suffix.Length];
        if (sequenceText.Length != 4 ||
            !sequenceText.All(char.IsDigit) ||
            !int.TryParse(sequenceText, out int sequence) ||
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
        ExecutionHandoffProcessing? handoffProcessing,
        string? failureReason)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            StartedAt = session.StartedAt,
            CompletedAt = completedAt,
            AcceptedAt = session.AcceptedAt,
            RejectedAt = session.RejectedAt,
            DecisionNote = session.DecisionNote,
            LastActivityAt = lastActivityAt,
            State = state,
            RepositoryState = repositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            PromptManifest = session.PromptManifest,
            RepositorySnapshot = session.RepositorySnapshot,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = handoffPath,
            HandoffProcessing = handoffProcessing ?? session.HandoffProcessing,
            FailureReason = failureReason,
            Events = session.Events
        };
    }
}

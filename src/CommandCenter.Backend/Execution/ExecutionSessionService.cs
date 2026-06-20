using System.Threading;

namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSessionService(
    IExecutionContextService executionContextService,
    IExecutionSessionStore sessionStore,
    IExecutionProvider executionProvider,
    IExecutionPromptBuilder promptBuilder) : IExecutionSessionService
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
    {
        var session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId)
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();

        return session?.RepositoryState ?? RepositoryExecutionState.Ready;
    }

    public async Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
    {
        var session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId && IsActiveRepositoryState(session.RepositoryState))
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();

        return session?.ToSummary();
    }

    public async Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MilestonePath))
        {
            throw new ArgumentException("Milestone path is required.", nameof(request));
        }

        await gate.WaitAsync();
        try
        {
            var sessions = (await sessionStore.LoadAsync()).ToList();
            if (sessions.Any(session => session.RepositoryId == repositoryId && IsActiveRepositoryState(session.RepositoryState)))
            {
                throw new InvalidOperationException("Repository already has an active execution session.");
            }

            var context = await executionContextService.BuildContextAsync(repositoryId, request.MilestonePath);
            if (context.Diagnostics.LaunchBlocked)
            {
                var reasons = context.Diagnostics.ValidationErrors.ToList();
                if (context.Diagnostics.HardLimitExceeded)
                {
                    reasons.Add("Execution context size hard limit was exceeded.");
                }

                throw new InvalidOperationException(
                    $"Execution launch is blocked: {string.Join(" ", reasons)}");
            }

            var startedAt = DateTimeOffset.UtcNow;
            var session = new ExecutionSession
            {
                Id = Guid.NewGuid(),
                RepositoryId = context.RepositoryId,
                RepositoryPath = context.RepositoryPath,
                MilestonePath = context.MilestonePath,
                StartedAt = startedAt,
                LastActivityAt = startedAt,
                State = ExecutionSessionState.Created,
                RepositoryState = RepositoryExecutionState.Ready,
                ProviderName = executionProvider.Name,
                RepositorySnapshot = context.RepositorySnapshot,
                PreviousHandoffContent = context.Artifacts
                    .SingleOrDefault(artifact => artifact.Role == "CurrentHandoff")
                    ?.Content,
                PreviousHandoffCapturedAt = context.Artifacts.Any(artifact => artifact.Role == "CurrentHandoff")
                    ? startedAt
                    : null
            };

            sessions.Add(session);
            await sessionStore.SaveAsync(sessions);

            var prompt = promptBuilder.Build(context);

            try
            {
                await executionProvider.StartAsync(prompt, session);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                var failedSession = session.WithState(
                    ExecutionSessionState.Failed,
                    RepositoryExecutionState.Ready,
                    completedAt: DateTimeOffset.UtcNow,
                    failureReason: exception.Message);
                await ReplaceSessionAsync(sessions, failedSession);
                return failedSession.ToSummary();
            }

            var executingSession = session.WithState(
                ExecutionSessionState.Executing,
                RepositoryExecutionState.Executing,
                lastActivityAt: DateTimeOffset.UtcNow);
            await ReplaceSessionAsync(sessions, executingSession);
            return executingSession.ToSummary();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
    {
        return (await sessionStore.LoadAsync()).FirstOrDefault(session => session.Id == sessionId);
    }

    private static bool IsActiveRepositoryState(RepositoryExecutionState state)
    {
        return state == RepositoryExecutionState.Executing;
    }

    private async Task ReplaceSessionAsync(List<ExecutionSession> sessions, ExecutionSession replacement)
    {
        var index = sessions.FindIndex(session => session.Id == replacement.Id);
        if (index < 0)
        {
            sessions.Add(replacement);
        }
        else
        {
            sessions[index] = replacement;
        }

        await sessionStore.SaveAsync(sessions);
    }
}

file static class ExecutionSessionMutation
{
    public static ExecutionSession WithState(
        this ExecutionSession session,
        ExecutionSessionState state,
        RepositoryExecutionState repositoryState,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? lastActivityAt = null,
        string? failureReason = null)
    {
        return new ExecutionSession
        {
            Id = session.Id,
            RepositoryId = session.RepositoryId,
            RepositoryPath = session.RepositoryPath,
            MilestonePath = session.MilestonePath,
            StartedAt = session.StartedAt,
            CompletedAt = completedAt ?? session.CompletedAt,
            LastActivityAt = lastActivityAt ?? session.LastActivityAt,
            State = state,
            RepositoryState = repositoryState,
            ProviderName = session.ProviderName,
            RepositorySnapshot = session.RepositorySnapshot,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            FailureReason = failureReason ?? session.FailureReason
        };
    }
}

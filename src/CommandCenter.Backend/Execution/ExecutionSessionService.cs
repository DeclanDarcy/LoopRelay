using System.Threading;

namespace CommandCenter.Backend.Execution;

public sealed class ExecutionSessionService(
    IExecutionContextService executionContextService,
    IExecutionSessionStore sessionStore,
    IExecutionProvider executionProvider,
    IExecutionPromptBuilder promptBuilder,
    IExecutionMonitoringService monitoringService) : IExecutionSessionService
{
    public const string OrphanedProviderFailureReason =
        "Active provider process could not be reattached after backend restart.";

    public const string ReattachedProviderRecoveryMessage =
        "Active provider process was reattached after backend restart.";

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task RecoverAsync()
    {
        await gate.WaitAsync();
        try
        {
            var sessions = (await sessionStore.LoadAsync()).ToList();
            var recoveredAt = DateTimeOffset.UtcNow;
            var changed = false;
            var reattachedSessionIds = new List<Guid>();

            for (var index = 0; index < sessions.Count; index++)
            {
                var session = sessions[index];
                if (session.RepositoryState != RepositoryExecutionState.Executing ||
                    session.State != ExecutionSessionState.Executing)
                {
                    continue;
                }

                if (executionProvider.SupportsReattach &&
                    await executionProvider.TryReattachAsync(
                        session,
                        monitoringService.CreateProviderObserver(session.Id)))
                {
                    sessions[index] = session.WithState(
                        ExecutionSessionState.Executing,
                        RepositoryExecutionState.Executing,
                        lastActivityAt: recoveredAt);
                    changed = true;
                    reattachedSessionIds.Add(session.Id);
                    continue;
                }

                sessions[index] = session.WithState(
                    ExecutionSessionState.Failed,
                    RepositoryExecutionState.Failed,
                    completedAt: recoveredAt,
                    lastActivityAt: recoveredAt,
                    failureReason: OrphanedProviderFailureReason);
                changed = true;
            }

            if (changed)
            {
                await sessionStore.SaveAsync(sessions);
                foreach (var sessionId in reattachedSessionIds)
                {
                    await monitoringService.RecordRecoveryAsync(sessionId, ReattachedProviderRecoveryMessage);
                }

                foreach (var session in sessions.Where(session => session.FailureReason == OrphanedProviderFailureReason))
                {
                    await monitoringService.RecordRecoveryAsync(session.Id, OrphanedProviderFailureReason);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

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

    public async Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId)
    {
        var session = (await sessionStore.LoadAsync())
            .Where(session => session.RepositoryId == repositoryId)
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

            ExecutionProviderStartResult startResult;
            try
            {
                startResult = await executionProvider.StartAsync(
                    prompt,
                    session,
                    monitoringService.CreateProviderObserver(session.Id));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                var failedSession = session.WithState(
                    ExecutionSessionState.Failed,
                    RepositoryExecutionState.Ready,
                    completedAt: DateTimeOffset.UtcNow,
                    failureReason: exception.Message);
                await ReplaceSessionAsync(sessions, failedSession);
                await monitoringService.RecordFailureAsync(session.Id, exception.Message);
                return failedSession.ToSummary();
            }

            var latestSession = (await sessionStore.LoadAsync()).FirstOrDefault(storedSession => storedSession.Id == session.Id) ?? session;
            var providerStartedAt = startResult.StartedAt == default ? DateTimeOffset.UtcNow : startResult.StartedAt;
            var executingSession = latestSession.WithState(
                ExecutionSessionState.Executing,
                RepositoryExecutionState.Executing,
                lastActivityAt: DateTimeOffset.UtcNow,
                providerExecutablePath: startResult.ExecutablePath,
                providerProcessId: startResult.ProcessId,
                providerStartedAt: providerStartedAt,
                promptMetadata: prompt.Metadata);
            await ReplaceSessionAsync(sessions, executingSession);
            await monitoringService.RecordProviderStartedAsync(session.Id, providerStartedAt);
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
        string? failureReason = null,
        string? providerExecutablePath = null,
        int? providerProcessId = null,
        DateTimeOffset? providerStartedAt = null,
        ExecutionPromptMetadata? promptMetadata = null)
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
            ProviderExecutablePath = providerExecutablePath ?? session.ProviderExecutablePath,
            ProviderProcessId = providerProcessId ?? session.ProviderProcessId,
            ProviderStartedAt = providerStartedAt ?? session.ProviderStartedAt,
            PromptMetadata = promptMetadata ?? session.PromptMetadata,
            RepositorySnapshot = session.RepositorySnapshot,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            FailureReason = failureReason ?? session.FailureReason,
            Events = session.Events
        };
    }
}

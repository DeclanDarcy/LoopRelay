namespace CommandCenter.Backend.Execution;

using System.Threading.Channels;
using System.Runtime.CompilerServices;

public sealed class ExecutionMonitoringService : IExecutionMonitoringService
{
    private readonly IExecutionSessionStore sessionStore;
    private readonly IHandoffService? handoffService;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ExecutionEventRetentionPolicy retentionPolicy;
    private readonly Dictionary<Guid, List<Channel<ExecutionEvent>>> subscribers = [];

    public ExecutionMonitoringService(IExecutionSessionStore sessionStore)
        : this(sessionStore, null, null)
    {
    }

    public ExecutionMonitoringService(
        IExecutionSessionStore sessionStore,
        ExecutionEventRetentionPolicy? retentionPolicy)
        : this(sessionStore, null, retentionPolicy)
    {
    }

    public ExecutionMonitoringService(
        IExecutionSessionStore sessionStore,
        IHandoffService? handoffService,
        ExecutionEventRetentionPolicy? retentionPolicy = null)
    {
        this.sessionStore = sessionStore;
        this.handoffService = handoffService;
        this.retentionPolicy = retentionPolicy ?? new ExecutionEventRetentionPolicy();
    }

    public IExecutionProviderObserver CreateProviderObserver(Guid sessionId)
    {
        return new ExecutionProviderObserver(this, sessionId);
    }

    public Task RecordProviderStartedAsync(Guid sessionId, DateTimeOffset startedAt)
    {
        return AppendEventAsync(
            sessionId,
            ExecutionEventType.ProviderStarted,
            "Provider process started.",
            activityAt: startedAt);
    }

    public Task RecordFailureAsync(Guid sessionId, string reason)
    {
        return AppendEventAsync(
            sessionId,
            ExecutionEventType.Failure,
            reason,
            activityAt: DateTimeOffset.UtcNow,
            mutateSession: session => CopySession(
                session,
                state: ExecutionSessionState.Failed,
                repositoryState: session.RepositoryState == RepositoryExecutionState.Executing
                    ? RepositoryExecutionState.Failed
                    : session.RepositoryState,
                completedAt: DateTimeOffset.UtcNow,
                failureReason: reason));
    }

    public Task RecordRecoveryAsync(Guid sessionId, string message)
    {
        return AppendEventAsync(sessionId, ExecutionEventType.Recovery, message, DateTimeOffset.UtcNow);
    }

    public Task RecordCancellationAsync(Guid sessionId, string reason)
    {
        return AppendEventAsync(
            sessionId,
            ExecutionEventType.Cancellation,
            reason,
            activityAt: DateTimeOffset.UtcNow,
            mutateSession: session => CopySession(
                session,
                state: ExecutionSessionState.Cancelled,
                repositoryState: RepositoryExecutionState.Cancelled,
                completedAt: DateTimeOffset.UtcNow));
    }

    public async Task<ExecutionStatus?> GetStatusAsync(Guid sessionId)
    {
        var session = (await sessionStore.LoadAsync()).FirstOrDefault(session => session.Id == sessionId);
        return session is null ? null : ToStatus(session);
    }

    public async Task<IReadOnlyList<ExecutionEvent>> GetEventsAsync(Guid sessionId)
    {
        var session = (await sessionStore.LoadAsync()).FirstOrDefault(session => session.Id == sessionId);
        return session?.Events ?? [];
    }

    public async IAsyncEnumerable<ExecutionEvent> StreamEventsAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<ExecutionEvent> channel;
        IReadOnlyList<ExecutionEvent> retainedEvents;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = (await sessionStore.LoadAsync()).FirstOrDefault(session => session.Id == sessionId);
            if (session is null)
            {
                yield break;
            }

            retainedEvents = session.Events;
            channel = Channel.CreateUnbounded<ExecutionEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            if (!subscribers.TryGetValue(sessionId, out var sessionSubscribers))
            {
                sessionSubscribers = [];
                subscribers[sessionId] = sessionSubscribers;
            }

            sessionSubscribers.Add(channel);
        }
        finally
        {
            gate.Release();
        }

        try
        {
            foreach (var executionEvent in retainedEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return executionEvent;
            }

            await foreach (var executionEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return executionEvent;
            }
        }
        finally
        {
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                if (subscribers.TryGetValue(sessionId, out var sessionSubscribers))
                {
                    sessionSubscribers.Remove(channel);
                    if (sessionSubscribers.Count == 0)
                    {
                        subscribers.Remove(sessionId);
                    }
                }

                channel.Writer.TryComplete();
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private Task RecordProviderOutputAsync(Guid sessionId, ExecutionEventType eventType, string text)
    {
        return AppendEventAsync(sessionId, eventType, text, DateTimeOffset.UtcNow);
    }

    private Task RecordProviderExitAsync(Guid sessionId, int? exitCode)
    {
        var message = exitCode is null
            ? "Provider process exited."
            : $"Provider process exited with code {exitCode}.";

        if (exitCode is not null and not 0)
        {
            return AppendEventAsync(
                sessionId,
                ExecutionEventType.ProviderExited,
                message,
                activityAt: DateTimeOffset.UtcNow,
                mutateSession: session => CopySession(
                    session,
                    state: ExecutionSessionState.Failed,
                    repositoryState: RepositoryExecutionState.Failed,
                    completedAt: DateTimeOffset.UtcNow,
                    failureReason: message));
        }

        return RecordSuccessfulProviderExitAsync(sessionId, message);
    }

    private async Task RecordSuccessfulProviderExitAsync(Guid sessionId, string message)
    {
        await AppendEventAsync(
            sessionId,
            ExecutionEventType.ProviderExited,
            message,
            activityAt: DateTimeOffset.UtcNow,
            mutateSession: session => CopySession(
                session,
                state: ExecutionSessionState.Completed,
                completedAt: DateTimeOffset.UtcNow));

        if (handoffService is not null)
        {
            await handoffService.ProcessProviderCompletionAsync(sessionId);
            var status = await GetStatusAsync(sessionId);
            if (status?.RepositoryState == RepositoryExecutionState.AwaitingAcceptance)
            {
                await AppendEventAsync(
                    sessionId,
                    ExecutionEventType.HandoffValidated,
                    "Current handoff validated for review.",
                    activityAt: DateTimeOffset.UtcNow);
            }
            else if (status?.RepositoryState == RepositoryExecutionState.Failed &&
                !string.IsNullOrWhiteSpace(status.FailureReason))
            {
                await AppendEventAsync(
                    sessionId,
                    ExecutionEventType.Failure,
                    status.FailureReason,
                    activityAt: DateTimeOffset.UtcNow);
            }
        }
    }

    private async Task AppendEventAsync(
        Guid sessionId,
        ExecutionEventType eventType,
        string message,
        DateTimeOffset? activityAt = null,
        Func<ExecutionSession, ExecutionSession>? mutateSession = null)
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
            var nextSequence = session.Events.Count == 0
                ? 1
                : session.Events.Max(executionEvent => executionEvent.Sequence) + 1;
            var executionEvent = new ExecutionEvent
            {
                Sequence = nextSequence,
                Timestamp = activityAt ?? DateTimeOffset.UtcNow,
                Type = eventType,
                Message = message
            };
            var events = ApplyRetention([.. session.Events, executionEvent]);
            var updatedSession = CopySession(
                mutateSession?.Invoke(session) ?? session,
                lastActivityAt: activityAt ?? session.LastActivityAt,
                events: events);
            sessions[index] = updatedSession;
            await sessionStore.SaveAsync(sessions);
            BroadcastEvent(sessionId, executionEvent);
        }
        finally
        {
            gate.Release();
        }
    }

    private IReadOnlyList<ExecutionEvent> ApplyRetention(IReadOnlyList<ExecutionEvent> events)
    {
        var retained = events
            .TakeLast(retentionPolicy.MaximumEventCount)
            .ToList();

        while (retained.Count > 0 && EstimateEventBytes(retained) > retentionPolicy.MaximumEventBytes)
        {
            retained.RemoveAt(0);
        }

        return retained;
    }

    private static int EstimateEventBytes(IEnumerable<ExecutionEvent> events)
    {
        return events.Sum(executionEvent =>
            sizeof(long) +
            32 +
            executionEvent.Type.ToString().Length +
            executionEvent.Message.Length * sizeof(char));
    }

    private void BroadcastEvent(Guid sessionId, ExecutionEvent executionEvent)
    {
        if (!subscribers.TryGetValue(sessionId, out var sessionSubscribers))
        {
            return;
        }

        foreach (var subscriber in sessionSubscribers.ToArray())
        {
            subscriber.Writer.TryWrite(executionEvent);
        }
    }

    private static ExecutionStatus ToStatus(ExecutionSession session)
    {
        return new ExecutionStatus
        {
            SessionId = session.Id,
            State = session.State,
            RepositoryState = session.RepositoryState,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            Duration = session.Duration,
            LastActivityAt = session.LastActivityAt,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            HandoffPath = session.HandoffPath,
            FailureReason = session.FailureReason,
            RecentEvents = session.Events
        };
    }

    private static ExecutionSession CopySession(
        ExecutionSession session,
        ExecutionSessionState? state = null,
        RepositoryExecutionState? repositoryState = null,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? lastActivityAt = null,
        string? failureReason = null,
        IReadOnlyList<ExecutionEvent>? events = null)
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
            State = state ?? session.State,
            RepositoryState = repositoryState ?? session.RepositoryState,
            ProviderName = session.ProviderName,
            ProviderExecutablePath = session.ProviderExecutablePath,
            ProviderProcessId = session.ProviderProcessId,
            ProviderStartedAt = session.ProviderStartedAt,
            PromptMetadata = session.PromptMetadata,
            RepositorySnapshot = session.RepositorySnapshot,
            PreviousHandoffContent = session.PreviousHandoffContent,
            PreviousHandoffCapturedAt = session.PreviousHandoffCapturedAt,
            HandoffPath = session.HandoffPath,
            FailureReason = failureReason ?? session.FailureReason,
            Events = events ?? session.Events
        };
    }

    private sealed class ExecutionProviderObserver(
        ExecutionMonitoringService monitoringService,
        Guid sessionId) : IExecutionProviderObserver
    {
        public Task OnStdOutAsync(string text)
        {
            return monitoringService.RecordProviderOutputAsync(sessionId, ExecutionEventType.StdOut, text);
        }

        public Task OnStdErrAsync(string text)
        {
            return monitoringService.RecordProviderOutputAsync(sessionId, ExecutionEventType.StdErr, text);
        }

        public Task OnProviderExitedAsync(int? exitCode)
        {
            return monitoringService.RecordProviderExitAsync(sessionId, exitCode);
        }

        public Task OnProviderCancelledAsync(string reason)
        {
            return monitoringService.RecordCancellationAsync(sessionId, reason);
        }
    }
}

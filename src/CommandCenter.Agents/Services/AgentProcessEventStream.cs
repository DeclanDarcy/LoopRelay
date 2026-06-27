using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

public sealed class AgentProcessEventStream
{
    private readonly Lock gate = new();
    private readonly TimeProvider timeProvider;
    private readonly List<AgentProcessEvent> events = [];
    private long nextSequence = 1;

    public AgentProcessEventStream(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<AgentProcessEvent> Events
    {
        get
        {
            lock (gate)
            {
                return events.ToArray();
            }
        }
    }

    public AgentProcessEvent Record(
        int processId,
        AgentProcessEventKind kind,
        AgentProcessState state,
        int? exitCode = null,
        AgentProcessOutputStream? outputStream = null,
        string? content = null,
        string? diagnosticCode = null,
        string? message = null)
    {
        lock (gate)
        {
            AgentProcessEvent processEvent = new(
                Guid.NewGuid(),
                processId,
                nextSequence,
                timeProvider.GetUtcNow(),
                kind,
                state,
                exitCode,
                outputStream,
                content,
                diagnosticCode,
                message);

            nextSequence++;
            events.Add(processEvent);
            return processEvent;
        }
    }

    public AgentProcessEvent? RecordIfAbsent(
        AgentProcessEventKind kind,
        int processId,
        AgentProcessState state,
        int? exitCode = null,
        AgentProcessOutputStream? outputStream = null,
        string? content = null,
        string? diagnosticCode = null,
        string? message = null)
    {
        lock (gate)
        {
            if (events.Any(processEvent => processEvent.Kind == kind))
            {
                return null;
            }

            AgentProcessEvent processEvent = new(
                Guid.NewGuid(),
                processId,
                nextSequence,
                timeProvider.GetUtcNow(),
                kind,
                state,
                exitCode,
                outputStream,
                content,
                diagnosticCode,
                message);

            nextSequence++;
            events.Add(processEvent);
            return processEvent;
        }
    }
}

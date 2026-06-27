using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

public sealed class AgentProcessSupervisor : IAgentProcessSupervisor
{
    private readonly IAgentProcess process;
    private readonly AgentProcessStateMachine stateMachine;
    private readonly AgentProcessEventStream eventStream;
    private readonly Task<AgentProcessSupervisionResult> completion;
    private bool disposed;

    public AgentProcessSupervisor(IAgentProcess process, AgentProcessEventStream? eventStream = null)
    {
        this.process = process;
        this.eventStream = eventStream ?? new AgentProcessEventStream();
        stateMachine = new AgentProcessStateMachine(process.State);
        ExitCode = process.ExitCode;
        this.eventStream.Record(
            process.ProcessId,
            AgentProcessEventKind.ProcessStarted,
            stateMachine.State,
            process.ExitCode);
        completion = CompleteAsync();
    }

    public AgentProcessState State => stateMachine.State;

    public int? ExitCode { get; private set; }

    public Task<AgentProcessSupervisionResult> Completion => completion;

    public IReadOnlyList<AgentProcessEvent> Events => eventStream.Events;

    public async Task<AgentProcessSupervisionResult> ObserveCompletionAsync(
        Func<int?, Task>? onExit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AgentProcessSupervisionResult result = await completion.WaitAsync(cancellationToken);
            ExitCode = result.ExitCode;
            stateMachine.TryTransitionTo(result.State);

            if (onExit is not null)
            {
                await onExit(result.ExitCode);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            stateMachine.TryTransitionTo(AgentProcessState.Failed);
            throw;
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        stateMachine.TransitionTo(AgentProcessState.Stopping);
        await process.DisposeAsync();
        ExitCode = process.ExitCode;
        stateMachine.TryTransitionTo(AgentProcessState.Canceled);
        eventStream.RecordIfAbsent(
            AgentProcessEventKind.ProcessCancelled,
            process.ProcessId,
            stateMachine.State,
            ExitCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (State is AgentProcessState.Running or AgentProcessState.Stopping)
        {
            await CancelAsync();
        }

        if (State is not AgentProcessState.Disposed)
        {
            stateMachine.TryTransitionTo(AgentProcessState.Disposed);
            eventStream.Record(
                process.ProcessId,
                AgentProcessEventKind.ProcessDisposed,
                stateMachine.State,
                ExitCode);
        }
    }

    private async Task<AgentProcessSupervisionResult> CompleteAsync()
    {
        try
        {
            await process.Completion;
        }
        catch
        {
            stateMachine.TryTransitionTo(AgentProcessState.Failed);
            eventStream.Record(
                process.ProcessId,
                AgentProcessEventKind.ProcessFailed,
                stateMachine.State,
                process.ExitCode,
                message: "Process completion failed.");
            throw;
        }

        ExitCode = process.ExitCode;
        stateMachine.TryTransitionTo(process.State);
        RecordTerminalCompletionEvent();

        return new AgentProcessSupervisionResult(
            process.State,
            process.ExitCode);
    }

    private void RecordTerminalCompletionEvent()
    {
        switch (stateMachine.State)
        {
            case AgentProcessState.Exited:
                eventStream.Record(
                    process.ProcessId,
                    AgentProcessEventKind.ProcessCompleted,
                    stateMachine.State,
                    ExitCode);
                break;
            case AgentProcessState.Canceled:
                eventStream.RecordIfAbsent(
                    AgentProcessEventKind.ProcessCancelled,
                    process.ProcessId,
                    stateMachine.State,
                    ExitCode);
                break;
        }
    }
}

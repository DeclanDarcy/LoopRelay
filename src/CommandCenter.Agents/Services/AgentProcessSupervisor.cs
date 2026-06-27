using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

public sealed class AgentProcessSupervisor : IAgentProcessSupervisor
{
    private readonly IAgentProcess process;
    private readonly AgentProcessStateMachine stateMachine;
    private readonly Task<AgentProcessSupervisionResult> completion;
    private bool disposed;

    public AgentProcessSupervisor(IAgentProcess process)
    {
        this.process = process;
        stateMachine = new AgentProcessStateMachine(process.State);
        ExitCode = process.ExitCode;
        completion = CompleteAsync();
    }

    public AgentProcessState State => stateMachine.State;

    public int? ExitCode { get; private set; }

    public Task<AgentProcessSupervisionResult> Completion => completion;

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
            throw;
        }

        ExitCode = process.ExitCode;
        stateMachine.TryTransitionTo(process.State);

        return new AgentProcessSupervisionResult(
            process.State,
            process.ExitCode);
    }
}

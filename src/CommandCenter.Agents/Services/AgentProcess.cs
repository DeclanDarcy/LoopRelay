using System.Diagnostics;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

internal sealed class AgentProcess(Process process) : IAgentProcess
{
    private readonly TaskCompletionSource completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ProcessId => process.Id;

    public AgentProcessState State { get; private set; } = AgentProcessState.Running;

    public int? ExitCode { get; private set; }

    public bool HasExited
    {
        get
        {
            CaptureExitStateIfAvailable();
            return process.HasExited;
        }
    }

    public Task Completion => completion.Task;

    internal StreamReader StandardOutput => process.StandardOutput;

    internal StreamReader StandardError => process.StandardError;

    public async Task ObserveExitAsync(Func<int?, Task>? onExit)
    {
        try
        {
            await process.WaitForExitAsync();
            CaptureExitStateIfAvailable();

            if (onExit is not null)
            {
                await onExit(ExitCode);
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            State = AgentProcessState.Failed;
            completion.TrySetException(exception);
            throw;
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default)
    {
        await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            State = AgentProcessState.Canceled;
        }

        process.Dispose();
        await ValueTask.CompletedTask;
    }

    private void CaptureExitStateIfAvailable()
    {
        if (!process.HasExited)
        {
            return;
        }

        ExitCode = process.ExitCode;
        State = AgentProcessState.Exited;
    }
}

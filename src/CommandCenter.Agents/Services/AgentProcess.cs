using System.Diagnostics;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

internal sealed class AgentProcess(Process process) : IAgentProcess
{
    private readonly TaskCompletionSource completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed;

    public AgentProcessState State { get; private set; } = AgentProcessState.Running;

    public int ProcessId => process.Id;

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

    public void StartCompletionObservation()
    {
        _ = Task.Run(ObserveExitAsync);
    }

    public async Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default)
    {
        await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            State = AgentProcessState.Canceled;
        }

        process.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task ObserveExitAsync()
    {
        try
        {
            await process.WaitForExitAsync();
            CaptureExitStateIfAvailable();
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            State = AgentProcessState.Failed;
            completion.TrySetException(exception);
        }
        finally
        {
            if (!disposed)
            {
                disposed = true;
                process.Dispose();
            }
        }
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

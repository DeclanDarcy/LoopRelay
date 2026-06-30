using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

internal sealed class AgentProcess(Process process) : IAgentProcess
{
    private readonly TaskCompletionSource completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed;
    private bool inputCompleted;
    private Task? errorDrain;

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

    /// <summary>
    /// Continuously drains the redirected standard-error stream so its OS pipe buffer can never fill.
    /// A redirected stream that nobody reads deadlocks the child once it writes past the buffer (a few KB);
    /// codex's non-JSON <c>exec</c> output is verbose enough to hit this on a real prompt, which wedges the
    /// whole turn (the turn completes only when stdout ends, and a blocked child can never reach that).
    /// The content is discarded — stdout carries the turn output; this exists purely to keep the pipe moving.
    /// </summary>
    public void StartErrorDrain()
    {
        errorDrain ??= Task.Run(DrainStandardErrorAsync);
    }

    private async Task DrainStandardErrorAsync()
    {
        try
        {
            char[] buffer = new char[4096];
            while (await process.StandardError.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0)
            {
            }
        }
        catch
        {
            // The process exited or the stream closed during teardown — nothing left to drain.
        }
    }

    public async Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        if (inputCompleted)
        {
            throw new InvalidOperationException("Cannot write to standard input after it has been completed.");
        }

        await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    public Task CompleteInputAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!inputCompleted)
        {
            inputCompleted = true;
            process.StandardInput.Close();
        }

        return Task.CompletedTask;
    }

    public async Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default)
    {
        await WritePromptAsync(standardInput, cancellationToken);
        await CompleteInputAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> ReadOutputLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            yield return line;
        }
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

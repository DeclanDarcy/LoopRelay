using System.Runtime.CompilerServices;
using System.Text;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Primitives.Process;

namespace LoopRelay.Agents.Services.Process;

internal sealed class AgentProcess(System.Diagnostics.Process _process) : IAgentProcess
{
    /// <summary>How much of the standard-error stream is retained for diagnostics (last-writer-wins tail).</summary>
    private const int ErrorTailCapacity = 8192;

    /// <summary>
    /// How long to wait, after stdout EOF, for the exit code to become observable. Stdout EOF means the
    /// child is exiting (or has closed its output), so this is a formality that closes a small race —
    /// it is bounded so a pathological child that closes stdout but lingers cannot stall the stream.
    /// </summary>
    private static readonly TimeSpan ExitCaptureTimeout = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object errorTailLock = new();
    private readonly StringBuilder errorTail = new();
    private bool disposed;
    private bool inputCompleted;
    private Task? errorDrain;

    public AgentProcessState State { get; private set; } = AgentProcessState.Running;

    public int ProcessId => _process.Id;

    public int? ExitCode { get; private set; }

    public bool HasExited
    {
        get
        {
            CaptureExitStateIfAvailable();
            return _process.HasExited;
        }
    }

    public Task Completion => completion.Task;

    /// <summary>
    /// The retained tail (last <see cref="ErrorTailCapacity"/> chars) of everything the drain read from
    /// standard error, or null when nothing was written. Thread-safe: the drain task appends while a
    /// session may read after stream end.
    /// </summary>
    public string? ErrorSnapshot
    {
        get
        {
            lock (errorTailLock)
            {
                return errorTail.Length == 0 ? null : errorTail.ToString();
            }
        }
    }

    internal StreamReader StandardOutput => _process.StandardOutput;

    internal StreamReader StandardError => _process.StandardError;

    public void StartCompletionObservation()
    {
        _ = Task.Run(ObserveExitAsync);
    }

    /// <summary>
    /// Continuously drains the redirected standard-error stream so its OS pipe buffer can never fill.
    /// A redirected stream that nobody reads deadlocks the child once it writes past the buffer (a few KB);
    /// codex's non-JSON <c>exec</c> output is verbose enough to hit this on a real prompt, which wedges the
    /// whole turn (the turn completes only when stdout ends, and a blocked child can never reach that).
    /// A bounded tail of the content is retained (exposed via <see cref="ErrorSnapshot"/>) so a failed
    /// turn can surface the child's error output — e.g. codex's "Not inside a trusted directory" refusal —
    /// instead of silently discarding it; the rest still exists purely to keep the pipe moving.
    /// </summary>
    public void StartErrorDrain()
    {
        errorDrain ??= Task.Run(DrainStandardErrorAsync);
    }

    private async Task DrainStandardErrorAsync()
    {
        try
        {
            // Capture the reader once: Process.Dispose nulls the StandardError property (making the
            // getter throw mid-drain) but does not close a sync-mode reader someone already holds, so
            // a cached reference can still drain the final buffered stderr to EOF after teardown.
            StreamReader reader = _process.StandardError;
            char[] buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
            {
                AppendErrorTail(buffer.AsSpan(0, read));
            }
        }
        catch
        {
            // The process exited or the stream closed during teardown — nothing left to drain.
        }
    }

    private void AppendErrorTail(ReadOnlySpan<char> chunk)
    {
        lock (errorTailLock)
        {
            errorTail.Append(chunk);
            if (errorTail.Length > ErrorTailCapacity)
            {
                errorTail.Remove(0, errorTail.Length - ErrorTailCapacity);
            }
        }
    }

    public async Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        if (inputCompleted)
        {
            throw new InvalidOperationException("Cannot write to standard input after it has been completed.");
        }

        await _process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    public Task CompleteInputAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!inputCompleted)
        {
            inputCompleted = true;
            _process.StandardInput.Close();
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
        while (await _process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            yield return line;
        }

        // Consumers treat stream end as the turn terminal and immediately consult ExitCode /
        // ErrorSnapshot, but stdout EOF can precede the exit code becoming observable by a beat.
        // Close that race before the enumeration completes.
        await CaptureExitAfterOutputEndAsync(cancellationToken);
    }

    /// <summary>
    /// Bounded wait for the exit code (and the stderr drain, when running) after stdout EOF, so both
    /// are reliably observable by the time the line stream completes. Caller cancellation propagates;
    /// the timeout (a child that closed stdout but lingers) leaves <see cref="ExitCode"/> null.
    /// </summary>
    private async Task CaptureExitAfterOutputEndAsync(CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(ExitCaptureTimeout);

        // The exit wait and the drain settle are guarded SEPARATELY: the exit observer can dispose
        // the Process concurrently (it captures ExitCode/State first), and the resulting throw here
        // must not skip the drain settle below — otherwise ErrorSnapshot is read while the drain may
        // still be appending the final buffered stderr chunk, truncating the diagnostics.
        try
        {
            if (ExitCode is null)
            {
                await _process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
                CaptureExitStateIfAvailable();
            }
        }
        catch (OperationCanceledException)
        {
            // Rethrow only genuine caller cancellation; a lapsed bounded wait is non-fatal.
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // The exit observer disposed the process during teardown — it captured the exit state
            // (ExitCode/State) before disposing, so there is nothing further to observe here.
        }

        if (errorDrain is { } drain)
        {
            try
            {
                // The process has exited (or was torn down), so stderr EOF is imminent — settle the
                // tail before it is read. The drain never faults (it swallows its own exceptions), so
                // only the bounded wait can throw here.
                await drain.WaitAsync(bounded.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Rethrow only genuine caller cancellation; a lapsed bounded wait is non-fatal.
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            State = AgentProcessState.Canceled;
        }

        _process.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task ObserveExitAsync()
    {
        try
        {
            await _process.WaitForExitAsync();
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
                _process.Dispose();
            }
        }
    }

    private void CaptureExitStateIfAvailable()
    {
        if (!_process.HasExited)
        {
            return;
        }

        ExitCode = _process.ExitCode;
        State = AgentProcessState.Exited;
    }
}

using System.Text;
using System.Threading.Channels;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Services;

/// <summary>
/// A single Codex-backed session that drives one underlying process across one or more turns.
/// Persistent sessions keep stdin open and complete each turn at a boundary marker; one-shot
/// sessions complete a single turn when the process output stream ends. Turns are serialized so
/// a held-open process is never asked to interleave prompts.
/// </summary>
public sealed class AgentSession : IAgentSession
{
    private readonly AgentSessionSpec spec;
    private readonly AgentSessionMode mode;
    private readonly IAgentProcess process;
    private readonly IAgentTurnBoundaryDetector boundaryDetector;
    private readonly IAgentTokenEstimator tokenEstimator;
    private readonly SemaphoreSlim turnGate = new(1, 1);
    private readonly Channel<string> lines = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource sessionCts = new();
    private readonly Task pumpTask;
    private int completedTurns;
    private AgentTokenUsage totalUsage = AgentTokenUsage.Zero;
    private bool disposed;

    public AgentSession(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        IAgentProcess process,
        IAgentTurnBoundaryDetector boundaryDetector,
        IAgentTokenEstimator tokenEstimator)
    {
        this.spec = spec;
        this.mode = mode;
        this.process = process;
        this.boundaryDetector = boundaryDetector;
        this.tokenEstimator = tokenEstimator;
        pumpTask = Task.Run(PumpOutputAsync);
    }

    public SessionIdentity SessionId => spec.SessionId;

    public string RepositoryId => spec.RepositoryId;

    public SessionRole Role => spec.Role;

    public AgentSessionMode Mode => mode;

    public AgentProcessState State => process.State;

    public int CompletedTurns => completedTurns;

    public AgentTokenUsage TotalUsage => totalUsage;

    public string? ThreadId => null; // one-shot/legacy path — no app-server thread exists

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);

        await turnGate.WaitAsync(linked.Token);

        try
        {
            int turnIndex = completedTurns + 1;

            AgentTurnProgress.Notify(observer => observer.RequestWriteStarted());
            await process.WritePromptAsync(EnsureTrailingNewline(prompt), linked.Token);

            if (mode == AgentSessionMode.OneShot)
            {
                await process.CompleteInputAsync(linked.Token);
            }

            AgentTurnProgress.Notify(observer => observer.RequestSubmitted());

            (string output, AgentTokenUsage? boundaryUsage, bool completed) =
                await ReadTurnAsync(turnIndex, onChunk, linked.Token);

            AgentTokenUsage usage = boundaryUsage
                ?? new AgentTokenUsage(tokenEstimator.Estimate(prompt), tokenEstimator.Estimate(output));

            completedTurns = turnIndex;
            totalUsage = totalUsage.Add(usage);

            return new AgentTurnResult(
                turnIndex,
                completed ? AgentTurnState.Completed : AgentTurnState.Failed,
                output,
                usage,
                // Failure-only diagnostics: the retained stderr tail explains WHY the process failed
                // (e.g. codex's "Not inside a trusted directory" refusal) instead of a bare state.
                completed ? null : process.ErrorSnapshot);
        }
        finally
        {
            turnGate.Release();
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await sessionCts.CancelAsync();
        await process.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (!sessionCts.IsCancellationRequested)
        {
            await sessionCts.CancelAsync();
        }

        try
        {
            if (mode == AgentSessionMode.Persistent && !process.HasExited)
            {
                await process.CompleteInputAsync();
            }
        }
        catch
        {
            // Best-effort stdin completion during teardown.
        }

        await process.DisposeAsync();

        try
        {
            await pumpTask;
        }
        catch
        {
            // The output pump terminates on cancellation or stream end during disposal.
        }

        sessionCts.Dispose();
        turnGate.Dispose();
    }

    private async Task<(string Output, AgentTokenUsage? Usage, bool Completed)> ReadTurnAsync(
        int turnIndex,
        Func<AgentStreamChunk, Task>? onChunk,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var renderedToolIds = new HashSet<string>(StringComparer.Ordinal);
        ChannelReader<string> reader = lines.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out string? line))
            {
                AgentTurnProgress.Notify(observer => observer.FirstProtocolEvent());

                // The boundary detector classifies every line in both modes: a real Codex turn
                // emits a turn-completed event before the process stream ends, while a one-shot
                // process that emits no boundary still terminates the turn on stream end below.
                AgentLineInspection inspection = boundaryDetector.Inspect(line);

                if (inspection.Classification == AgentLineClassification.TurnCompleted)
                {
                    return (output.ToString(), inspection.Usage, true);
                }

                if (inspection.Classification == AgentLineClassification.ToolCall)
                {
                    if (inspection.StreamId is { Length: > 0 } streamId && !renderedToolIds.Add(streamId))
                    {
                        continue;
                    }

                    if (onChunk is not null && inspection.Content is { Length: > 0 } toolSummary)
                    {
                        await onChunk(new AgentStreamChunk(
                            turnIndex,
                            AgentProcessOutputStream.StandardOutput,
                            toolSummary,
                            AgentStreamChunkKind.ToolCall));
                    }

                    continue;
                }

                if (inspection.Classification == AgentLineClassification.Ignored)
                {
                    continue;
                }

                // Output text is the detector's extracted content (e.g. an agent-message event's
                // text) when provided, otherwise the raw line (sentinel/plain-text streams).
                string text = inspection.Content ?? line;

                if (output.Length > 0)
                {
                    output.Append('\n');
                }

                output.Append(text);

                if (onChunk is not null)
                {
                    await onChunk(new AgentStreamChunk(
                        turnIndex,
                        AgentProcessOutputStream.StandardOutput,
                        text));
                }
            }
        }

        // Stream ended: the normal terminal for one-shot — but only when the process actually
        // SUCCEEDED. A one-shot that exits nonzero without emitting a boundary (e.g. codex refusing
        // to run at all) previously mapped to Completed because the exit code was never consulted.
        // A null exit code means the process cannot report one (fakes/unknown) — legacy behavior.
        // For persistent sessions a stream end is always an unexpected exit.
        return (output.ToString(), null, mode == AgentSessionMode.OneShot && process.ExitCode is null or 0);
    }

    private async Task PumpOutputAsync()
    {
        try
        {
            await foreach (string line in process.ReadOutputLinesAsync(sessionCts.Token))
            {
                lines.Writer.TryWrite(line);
            }

            lines.Writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            lines.Writer.TryComplete();
        }
        catch (Exception exception)
        {
            lines.Writer.TryComplete(exception);
        }
    }

    private static string EnsureTrailingNewline(string prompt) =>
        prompt.EndsWith('\n') ? prompt : prompt + "\n";
}

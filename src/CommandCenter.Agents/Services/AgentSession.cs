using System.Text;
using System.Threading.Channels;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

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

            await process.WritePromptAsync(EnsureTrailingNewline(prompt), linked.Token);

            if (mode == AgentSessionMode.OneShot)
            {
                await process.CompleteInputAsync(linked.Token);
            }

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
                usage);
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
        ChannelReader<string> reader = lines.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out string? line))
            {
                if (mode == AgentSessionMode.Persistent)
                {
                    AgentLineInspection inspection = boundaryDetector.Inspect(line);
                    if (inspection.Classification == AgentLineClassification.TurnCompleted)
                    {
                        return (output.ToString(), inspection.Usage, true);
                    }
                }

                if (output.Length > 0)
                {
                    output.Append('\n');
                }

                output.Append(line);

                if (onChunk is not null)
                {
                    await onChunk(new AgentStreamChunk(
                        turnIndex,
                        AgentProcessOutputStream.StandardOutput,
                        line));
                }
            }
        }

        // Stream ended: a normal terminal for one-shot, an unexpected exit for persistent.
        return (output.ToString(), null, mode == AgentSessionMode.OneShot);
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

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>
/// A held-open Codex session over the app-server JSON-RPC protocol (codex-cli 0.139). One process
/// serves many turns. Three roles are decoupled so none can block another:
/// <list type="bullet">
/// <item>a <b>read pump</b> drains stdout, parses frames, and only ever does non-blocking work —
/// correlating responses to requests by id, routing notifications to the active turn, and enqueueing
/// approval replies;</item>
/// <item>a <b>write loop</b> is the sole writer of stdin, draining an outbound channel so neither the
/// pump nor a turn ever blocks on a stdin write (avoiding stdin/stdout pipe deadlock);</item>
/// <item>each <b>turn</b> drains its own chunk channel to surface streaming deltas, so an untrusted
/// <c>onChunk</c> callback runs on the caller's path and can never wedge the transport.</item>
/// </list>
/// The first turn lazily runs the <c>initialize</c> → <c>initialized</c> → <c>thread/start</c> (or
/// <c>thread/resume</c> when the spec carries a ResumeThreadId; the resume path runs it eagerly via
/// EnsureReadyAsync) handshake; later turns reuse the thread. Turns are serialized so the one process is never asked to
/// interleave prompts. When the process exits the pump cancels the session so every awaiter is released.
/// </summary>
public sealed class CodexAppServerSession : IAgentSession
{
    private readonly AgentSessionSpec spec;
    private readonly IAgentProcess process;
    private readonly IAgentTokenEstimator tokenEstimator;

    private readonly SemaphoreSlim turnGate = new(1, 1);
    private readonly Channel<string> outbound = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<long, TaskCompletionSource<CodexAppServerMessage>> pending = new();
    private readonly CancellationTokenSource sessionCts = new();
    private readonly Task pumpTask;
    private readonly Task writerTask;

    private long nextId;
    private bool initialized;
    private string? threadId;
    private volatile ActiveTurn? activeTurn;
    private volatile bool pumpEnded;
    private volatile int completedTurns;
    private AgentTokenUsage totalUsage = AgentTokenUsage.Zero;
    private volatile bool disposed;

    public CodexAppServerSession(
        AgentSessionSpec spec,
        IAgentProcess process,
        IAgentTokenEstimator tokenEstimator)
    {
        this.spec = spec;
        this.process = process;
        this.tokenEstimator = tokenEstimator;
        pumpTask = Task.Run(PumpAsync);
        writerTask = Task.Run(WriteLoopAsync);
    }

    public SessionIdentity SessionId => spec.SessionId;

    public string RepositoryId => spec.RepositoryId;

    public SessionRole Role => spec.Role;

    public AgentSessionMode Mode => AgentSessionMode.Persistent;

    public AgentProcessState State => process.State;

    public int CompletedTurns => completedTurns;

    public AgentTokenUsage TotalUsage => Volatile.Read(ref totalUsage);

    public string? ThreadId => threadId;

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
            await EnsureHandshakeAsync(linked.Token);

            int turnIndex = completedTurns + 1;
            var turn = new ActiveTurn(turnIndex);
            activeTurn = turn;
            try
            {
                long requestId = NextId();
                CodexAppServerMessage ack = await SendRequestAsync(
                    requestId,
                    CodexAppServerProtocol.TurnStart(requestId, threadId!, prompt, MapEffort(spec.Effort)),
                    linked.Token);
                ThrowIfError(ack, "turn/start");

                // Deltas are surfaced on this (caller) path, never on the pump, so onChunk cannot
                // wedge the transport. The pump completes the chunk channel when the turn ends.
                await foreach (AgentStreamChunk chunk in turn.Chunks.Reader.ReadAllAsync(linked.Token))
                {
                    if (onChunk is not null)
                    {
                        await onChunk(chunk);
                    }
                }

                await turn.Completion.Task.WaitAsync(linked.Token);
            }
            finally
            {
                activeTurn = null;
            }

            CodexAppServerTurnOutcome outcome = turn.Reader.Result();
            AgentTokenUsage usage = outcome.Usage
                ?? new AgentTokenUsage(tokenEstimator.Estimate(prompt), tokenEstimator.Estimate(outcome.Output));

            completedTurns = turnIndex;
            Volatile.Write(ref totalUsage, totalUsage.Add(usage));

            // Failure-only diagnostics: prefer the protocol-level failure message (turn/completed error), fall
            // back to the process's retained stderr tail — so consumers see WHY codex failed, not a bare state.
            return new AgentTurnResult(
                turnIndex,
                outcome.State,
                outcome.Output,
                usage,
                outcome.State == AgentTurnState.Completed
                    ? null
                    : NonWhitespace(outcome.FailureMessage) ?? NonWhitespace(process.ErrorSnapshot));
        }
        finally
        {
            turnGate.Release();
        }
    }

    /// <summary>
    /// Runs the handshake eagerly. Used by the resume path: the caller must know whether the resume succeeded
    /// BEFORE composing its first prompt (priming is decided at prompt-build time). Normal sessions keep the
    /// lazy first-turn handshake; calling this on one is a harmless no-op after the first time.
    /// </summary>
    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);

        await turnGate.WaitAsync(linked.Token);
        try
        {
            await EnsureHandshakeAsync(linked.Token);
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

        try
        {
            await sessionCts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed elsewhere.
        }

        outbound.Writer.TryComplete();
        await process.DisposeAsync();

        try
        {
            // The pump/writer end on cancellation or stream close; bound the wait so a stuck process
            // (or, defensively, anything parked) cannot wedge disposal.
            await Task.WhenAll(pumpTask, writerTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Timeout or faulted background task during teardown — proceed regardless.
        }

        foreach (TaskCompletionSource<CodexAppServerMessage> waiter in pending.Values)
        {
            waiter.TrySetCanceled();
        }

        sessionCts.Dispose();

        // turnGate is intentionally not disposed: a SemaphoreSlim holds no scarce handle unless its
        // AvailableWaitHandle is used (it is not), and disposing it could race an unwinding turn's
        // Release with an ObjectDisposedException. GC reclaims it.
    }

    private async Task EnsureHandshakeAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        long initId = NextId();
        CodexAppServerMessage initResponse = await SendRequestAsync(
            initId, CodexAppServerProtocol.Initialize(initId), cancellationToken);
        ThrowIfError(initResponse, "initialize");

        Enqueue(CodexAppServerProtocol.Initialized());

        long threadRequestId = NextId();
        bool resuming = spec.ResumeThreadId is { Length: > 0 };
        string threadFrame = resuming
            ? CodexAppServerProtocol.ThreadResume(
                threadRequestId, spec.ResumeThreadId!, spec.WorkingDirectory, Sandbox(), ApprovalPolicy())
            : CodexAppServerProtocol.ThreadStart(threadRequestId, spec.WorkingDirectory, Sandbox(), ApprovalPolicy());
        CodexAppServerMessage threadResponse = await SendRequestAsync(threadRequestId, threadFrame, cancellationToken);
        if (resuming && threadResponse.ErrorMessage is { } resumeError)
        {
            // A rejected resume (rollout deleted, unknown thread, protocol drift) is RECOVERABLE: the typed
            // exception lets the runtime tear this process down and the caller fall back to a fresh thread.
            throw new AgentSessionResumeException($"Codex thread/resume failed: {resumeError}");
        }

        ThrowIfError(threadResponse, "thread/start");

        string? extractedThreadId = ExtractThreadId(threadResponse.Result);
        if (extractedThreadId is null)
        {
            if (resuming)
            {
                throw new AgentSessionResumeException("Codex thread/resume response did not contain a thread id.");
            }

            throw new InvalidOperationException("Codex thread/start response did not contain a thread id.");
        }

        threadId = extractedThreadId;
        initialized = true;
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (string line in process.ReadOutputLinesAsync(sessionCts.Token))
            {
                Dispatch(CodexAppServerMessage.Parse(line));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation/disposal.
        }
        catch
        {
            // Awaiters are released in the finally block below.
        }
        finally
        {
            pumpEnded = true;

            ActiveTurn? turn = activeTurn;
            if (turn is not null)
            {
                turn.Completion.TrySetResult(false);
                turn.Chunks.Writer.TryComplete();
            }

            foreach (TaskCompletionSource<CodexAppServerMessage> waiter in pending.Values)
            {
                waiter.TrySetException(new IOException("Codex app-server stream ended before a response arrived."));
            }

            // Release any awaiter still on a session-linked token even when the caller passed None.
            try
            {
                sessionCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Disposed concurrently.
            }
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (string frame in outbound.Reader.ReadAllAsync(sessionCts.Token))
            {
                await process.WritePromptAsync(frame, sessionCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation/disposal.
        }
        catch
        {
            // Process gone; the pump's finally releases awaiters.
        }
    }

    private void Dispatch(CodexAppServerMessage message)
    {
        switch (message.Kind)
        {
            case CodexAppServerMessageKind.Response:
                if (message.NumericId is long id
                    && pending.TryRemove(id, out TaskCompletionSource<CodexAppServerMessage>? waiter))
                {
                    waiter.TrySetResult(message);
                }

                break;

            case CodexAppServerMessageKind.ServerRequest:
                // Approvals shouldn't fire (approvalPolicy "never"), but an unanswered request hangs
                // the turn — enqueue a decline without blocking the read loop.
                if (message.Id is not null)
                {
                    Enqueue(CodexAppServerProtocol.ApprovalResponse(message.Id, CodexAppServerProtocol.DeclineDecision));
                }

                break;

            case CodexAppServerMessageKind.Notification:
                ActiveTurn? turn = activeTurn;
                if (turn is null)
                {
                    break;
                }

                if (turn.Reader.Apply(message) is { } emission && !string.IsNullOrEmpty(emission.Text))
                {
                    turn.Chunks.Writer.TryWrite(new AgentStreamChunk(
                        turn.TurnIndex, AgentProcessOutputStream.StandardOutput, emission.Text, emission.Kind));
                }

                if (turn.Reader.IsComplete)
                {
                    turn.Completion.TrySetResult(true);
                    turn.Chunks.Writer.TryComplete();
                }

                break;
        }
    }

    private async Task<CodexAppServerMessage> SendRequestAsync(long id, string frame, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<CodexAppServerMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;

        // Close the race where the pump's terminal drain ran before this registration.
        if (pumpEnded)
        {
            pending.TryRemove(id, out _);
            completion.TrySetException(new IOException("Codex app-server is not running."));
        }

        await using CancellationTokenRegistration registration =
            cancellationToken.Register(() => completion.TrySetCanceled());
        try
        {
            Enqueue(frame);
            return await completion.Task;
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    private void Enqueue(string frame) => outbound.Writer.TryWrite(frame + "\n");

    private long NextId() => Interlocked.Increment(ref nextId);

    // The Identifier IS the codex sandbox mode (read-only | workspace-write | danger-full-access). Emit it
    // verbatim so every mode is reachable — a prior bool mapping could only ever produce the first two.
    private string Sandbox() => spec.Sandbox.Identifier;

    private string? ApprovalPolicy() => spec.Sandbox.RequiresApproval ? null : "never";

    private static string MapEffort(EffortProfile effort) =>
        effort.Identifier is { Length: > 0 } identifier
            ? identifier
            : effort.Level switch
            {
                AgentEffortLevel.High => "high",
                AgentEffortLevel.Medium => "medium",
                _ => "low"
            };

    private static string? NonWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void ThrowIfError(CodexAppServerMessage message, string context)
    {
        if (message.ErrorMessage is { } error)
        {
            throw new InvalidOperationException($"Codex {context} failed: {error}");
        }
    }

    private static string? ExtractThreadId(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object
        && result.TryGetProperty("thread", out JsonElement thread) && thread.ValueKind == JsonValueKind.Object
        && thread.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    private sealed class ActiveTurn(int turnIndex)
    {
        public CodexAppServerTurnReader Reader { get; } = new();

        public int TurnIndex { get; } = turnIndex;

        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Channel<AgentStreamChunk> Chunks { get; } = Channel.CreateUnbounded<AgentStreamChunk>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }
}

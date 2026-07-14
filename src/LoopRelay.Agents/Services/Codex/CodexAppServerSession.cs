using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Codex;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Codex;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Permissions.Abstractions.Evaluation;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Evaluation;

namespace LoopRelay.Agents.Services.Codex;

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
    private readonly AgentSessionSpec _spec;
    private readonly IAgentProcess _process;
    private readonly IAgentTokenEstimator _tokenEstimator;
    private readonly IPermissionGateway? _permissionGateway;
    private readonly SessionContinuityProfile? _continuityProfile;

    private readonly SemaphoreSlim turnGate = new(1, 1);
    private readonly Channel<string> outbound = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<long, TaskCompletionSource<CodexAppServerMessage>> pending = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> fileChangeTargets = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource sessionCts = new();
    private readonly Task pumpTask;
    private readonly Task writerTask;

    private long nextId;
    private bool initialized;
    private string? threadId;
    private JsonElement initializeResult;
    private volatile ActiveTurn? activeTurn;
    private volatile bool pumpEnded;
    private volatile int completedTurns;
    private AgentTokenUsage totalUsage = AgentTokenUsage.Zero;
    private volatile bool disposed;

    public CodexAppServerSession(
        AgentSessionSpec spec,
        IAgentProcess process,
        IAgentTokenEstimator tokenEstimator,
        IPermissionGateway? permissionGateway = null,
        SessionContinuityProfile? continuityProfile = null)
    {
        _spec = spec;
        _process = process;
        _tokenEstimator = tokenEstimator;
        _permissionGateway = permissionGateway;
        _continuityProfile = continuityProfile;
        pumpTask = Task.Run(PumpAsync);
        writerTask = Task.Run(WriteLoopAsync);
    }

    public SessionIdentity SessionId => _spec.SessionId;

    public string RepositoryId => _spec.RepositoryId;

    public SessionRole Role => _spec.Role;

    public AgentSessionMode Mode => AgentSessionMode.Persistent;

    public AgentProcessState State => _process.State;

    public int CompletedTurns => completedTurns;

    public AgentTokenUsage TotalUsage => Volatile.Read(ref totalUsage);

    public string? ThreadId => threadId;

    public SessionContinuityProfile? ContinuityProfile => _continuityProfile;

    public JsonElement InitializeResult => initializeResult;

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
            IAgentTurnProgressObserver? progress = AgentTurnProgress.Current;
            var turn = new ActiveTurn(turnIndex, progress);
            activeTurn = turn;
            try
            {
                long requestId = NextId();
                CodexAppServerMessage ack = await SendRequestAsync(
                    requestId,
                    CodexAppServerProtocol.TurnStart(
                        requestId,
                        threadId!,
                        prompt,
                        AgentConfigurationCatalog.Format(_spec.Model),
                        AgentConfigurationCatalog.Format(_spec.Effort)),
                    linked.Token,
                    () => AgentTurnProgress.Notify(progress, observer => observer.RequestWriteStarted()),
                    () => AgentTurnProgress.Notify(progress, observer => observer.RequestSubmitted()));
                ThrowIfError(ack, "turn/start", requestId);
                AgentTurnProgress.Notify(progress, observer => observer.RequestAccepted());

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
            if (outcome.ProviderTurnId is { Length: > 0 } providerTurnId)
            {
                AgentTurnProgress.Notify(progress, observer => observer.ProviderTurnIdentified(providerTurnId));
            }
            AgentTurnProgress.Notify(progress, observer => observer.Terminal());
            AgentTokenUsage usage = outcome.Usage
                ?? new AgentTokenUsage(_tokenEstimator.Estimate(prompt), _tokenEstimator.Estimate(outcome.Output));

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
                    : NonWhitespace(outcome.FailureMessage) ?? NonWhitespace(_process.ErrorSnapshot),
                outcome.ProviderTurnId,
                AgentTurnTransportState.Terminal);
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

    public async Task<CodexThreadReadResult> ReadThreadAsync(
        string requestedThreadId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (_continuityProfile is null)
        {
            throw new SessionOperationProfileGateException("A captured continuity profile is required for thread/read.");
        }

        CodexThreadReadOptions options = CodexThreadReadOptions.FromProfile(_continuityProfile, requestedThreadId);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);
        await turnGate.WaitAsync(linked.Token);
        try
        {
            await EnsureInitializedAsync(linked.Token);
            long requestId = NextId();
            CodexAppServerMessage response = await SendRequestAsync(
                requestId,
                CodexAppServerProtocol.ThreadRead(requestId, options),
                linked.Token);
            ThrowIfError(response, "thread/read", requestId);
            return new CodexThreadReadParser().Parse(response.Result, requestedThreadId);
        }
        finally
        {
            turnGate.Release();
        }
    }

    public async Task<(string ChildThreadId, string? ParentThreadId, string? HistoryDigest)> ForkThreadAsync(
        string parentThreadId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (_continuityProfile is null)
        {
            throw new SessionOperationProfileGateException("A captured continuity profile is required for thread/fork.");
        }

        CodexThreadForkOptions options = CodexThreadForkOptions.FromProfile(
            _continuityProfile, parentThreadId, _spec.WorkingDirectory, Sandbox(), ApprovalPolicy());
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);
        await turnGate.WaitAsync(linked.Token);
        try
        {
            await EnsureInitializedAsync(linked.Token);
            long requestId = NextId();
            CodexAppServerMessage response = await SendRequestAsync(
                requestId,
                CodexAppServerProtocol.ThreadFork(requestId, options),
                linked.Token);
            ThrowIfError(response, "thread/fork", requestId);
            string? child = ExtractThreadId(response.Result);
            if (child is null || string.Equals(child, parentThreadId, StringComparison.Ordinal))
            {
                throw new CodexAppServerRequestException(
                    "Codex thread/fork did not return a distinct child thread id.",
                    "thread/fork",
                    requestId,
                    response);
            }

            string? reportedParent = ExtractString(response.Result, "parentThreadId")
                ?? (response.Result.TryGetProperty("thread", out JsonElement thread)
                    ? ExtractString(thread, "parentThreadId")
                    : null);
            if (reportedParent is not null && !string.Equals(reportedParent, parentThreadId, StringComparison.Ordinal))
            {
                throw new CodexAppServerRequestException(
                    "Codex thread/fork returned a mismatched parent thread id.",
                    "thread/fork",
                    requestId,
                    response);
            }

            string? historyDigest = ExtractString(response.Result, "historyDigest")
                ?? (response.Result.TryGetProperty("thread", out thread)
                    ? ExtractString(thread, "historyDigest")
                    : null);
            threadId = child;
            initialized = true;
            return (child, reportedParent, historyDigest);
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
        await _process.DisposeAsync();
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
        await _process.DisposeAsync();

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

        bool resuming = _spec.ResumeThreadId is { Length: > 0 };
        CodexThreadResumeOptions? resumeOptions = null;
        if (resuming)
        {
            if (_continuityProfile is null)
            {
                throw new SessionOperationProfileGateException(
                    "A captured SessionContinuityProfile is required before thread/resume can be attempted.");
            }

            resumeOptions = CodexThreadResumeOptions.FromProfile(
                _continuityProfile,
                _spec.ResumeThreadId!,
                _spec.WorkingDirectory,
                Sandbox(),
                ApprovalPolicy(),
                AgentConfigurationCatalog.Format(_spec.Model));
        }

        await EnsureInitializedAsync(cancellationToken);

        long threadRequestId = NextId();
        string threadFrame = resuming
            ? CodexAppServerProtocol.ThreadResume(threadRequestId, resumeOptions!)
            : CodexAppServerProtocol.ThreadStart(
                threadRequestId,
                _spec.WorkingDirectory,
                Sandbox(),
                ApprovalPolicy(),
                AgentConfigurationCatalog.Format(_spec.Model));
        CodexAppServerMessage threadResponse = await SendRequestAsync(threadRequestId, threadFrame, cancellationToken);
        ThrowIfError(threadResponse, resuming ? "thread/resume" : "thread/start", threadRequestId);

        string? extractedThreadId = ExtractThreadId(threadResponse.Result);
        if (extractedThreadId is null)
        {
            if (resuming)
            {
                throw new CodexAppServerRequestException(
                    "Codex thread/resume response did not contain a thread id.",
                    "thread/resume",
                    threadRequestId,
                    threadResponse);
            }

            throw new InvalidOperationException("Codex thread/start response did not contain a thread id.");
        }

        threadId = extractedThreadId;
        initialized = true;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initializeResult.ValueKind != JsonValueKind.Undefined)
        {
            return;
        }

        long initId = NextId();
        CodexAppServerMessage initResponse = await SendRequestAsync(
            initId,
            CodexAppServerProtocol.Initialize(
                initId,
                _continuityProfile is null
                    ? new CodexInitializeOptions(ExperimentalApi: false)
                    : CodexInitializeOptions.FromProfile(_continuityProfile)),
            cancellationToken);
        ThrowIfError(initResponse, "initialize", initId);
        initializeResult = initResponse.Result.Clone();
        Enqueue(CodexAppServerProtocol.Initialized());
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (string line in _process.ReadOutputLinesAsync(sessionCts.Token))
            {
                Dispatch(line, CodexAppServerMessage.Parse(line));
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
                await _process.WritePromptAsync(frame, sessionCts.Token);
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

    private void Dispatch(string rawLine, CodexAppServerMessage message)
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
                if (message.Id is not null)
                {
                    EnqueueApprovalResponse(rawLine, message);
                }

                break;

            case CodexAppServerMessageKind.Notification:
                ObserveFileChangeTargets(message);
                ActiveTurn? turn = activeTurn;
                if (turn is null)
                {
                    break;
                }

                AgentTurnProgress.Notify(turn.Progress, observer => observer.FirstProtocolEvent());

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

    private async Task<CodexAppServerMessage> SendRequestAsync(
        long id,
        string frame,
        CancellationToken cancellationToken,
        Action? beforeEnqueue = null,
        Action? afterEnqueue = null)
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
            beforeEnqueue?.Invoke();
            Enqueue(frame);
            afterEnqueue?.Invoke();
            return await completion.Task;
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    private void Enqueue(string frame) => outbound.Writer.TryWrite(frame + "\n");

    private void EnqueueCompleteFrame(string frame)
    {
        outbound.Writer.TryWrite(frame.EndsWith('\n') ? frame : frame + "\n");
    }

    private void EnqueueApprovalResponse(string rawLine, CodexAppServerMessage message)
    {
        if (_permissionGateway is null)
        {
            Enqueue(CodexAppServerProtocol.ApprovalResponse(message.Id!, CodexAppServerProtocol.DeclineDecision));
            return;
        }

        try
        {
            rawLine = EnrichFileChangeApproval(rawLine, message);
            byte[] response = _permissionGateway.Evaluate(
                Encoding.UTF8.GetBytes(rawLine),
                new PermissionGatewayContext(
                    _spec.RepositoryId,
                    _spec.WorkingDirectory ?? ".",
                    _spec.OperationPermissionProfile));
            EnqueueCompleteFrame(Encoding.UTF8.GetString(response));
        }
        catch
        {
            Enqueue(CodexAppServerProtocol.ApprovalResponse(message.Id!, CodexAppServerProtocol.DeclineDecision));
        }
    }

    private void ObserveFileChangeTargets(CodexAppServerMessage message)
    {
        if (!string.Equals(message.Method, "item/started", StringComparison.Ordinal) ||
            message.Params.ValueKind != JsonValueKind.Object ||
            !message.Params.TryGetProperty("item", out JsonElement item) ||
            item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("type", out JsonElement type) ||
            type.GetString() != "fileChange" ||
            !item.TryGetProperty("id", out JsonElement id) ||
            id.ValueKind != JsonValueKind.String ||
            !item.TryGetProperty("changes", out JsonElement changes) ||
            changes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        string[] targets = changes.EnumerateArray()
            .Where(change => change.ValueKind == JsonValueKind.Object &&
                change.TryGetProperty("path", out JsonElement path) &&
                path.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(path.GetString()))
            .Select(change => change.GetProperty("path").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (targets.Length > 0)
        {
            fileChangeTargets[id.GetString()!] = targets;
        }
    }

    private string EnrichFileChangeApproval(string rawLine, CodexAppServerMessage message)
    {
        if (!string.Equals(message.Method, "item/fileChange/requestApproval", StringComparison.Ordinal) ||
            message.Params.ValueKind != JsonValueKind.Object ||
            !message.Params.TryGetProperty("itemId", out JsonElement itemId) ||
            itemId.ValueKind != JsonValueKind.String ||
            !fileChangeTargets.TryGetValue(itemId.GetString()!, out IReadOnlyList<string>? targets) ||
            targets.Count == 0)
        {
            return rawLine;
        }

        JsonNode? root = JsonNode.Parse(rawLine);
        JsonObject? parameters = root?["params"] as JsonObject;
        if (parameters is null)
        {
            return rawLine;
        }

        if (targets.Count == 1)
        {
            if (parameters["targetPath"] is not JsonValue existing ||
                !existing.TryGetValue(out string? existingPath) ||
                string.IsNullOrWhiteSpace(existingPath))
            {
                parameters["targetPath"] = targets[0];
            }
        }
        else
        {
            parameters["targetPaths"] = new JsonArray(
                targets.Select(target => JsonValue.Create(target)).ToArray());
        }
        return root!.ToJsonString();
    }

    private long NextId() => Interlocked.Increment(ref nextId);

    // The Identifier IS the codex sandbox mode (read-only | workspace-write | danger-full-access). Emit it
    // verbatim so every mode is reachable — a prior bool mapping could only ever produce the first two.
    private string Sandbox() => _spec.Sandbox.Identifier;

    private string? ApprovalPolicy() => _spec.Sandbox.RequiresApproval ? null : "never";

    private static string? NonWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void ThrowIfError(CodexAppServerMessage message, string context, long requestId)
    {
        if (message.ErrorCode is not null || message.ErrorMessage is not null)
        {
            throw new CodexAppServerRequestException(
                $"Codex {context} failed.",
                context,
                requestId,
                message);
        }
    }

    private static string? ExtractThreadId(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object
        && result.TryGetProperty("thread", out JsonElement thread) && thread.ValueKind == JsonValueKind.Object
        && thread.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    private static string? ExtractString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(property, out JsonElement element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private sealed class ActiveTurn(int _turnIndex, IAgentTurnProgressObserver? _progress)
    {
        public CodexAppServerTurnReader Reader { get; } = new();

        public int TurnIndex { get; } = _turnIndex;

        public IAgentTurnProgressObserver? Progress { get; } = _progress;

        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Channel<AgentStreamChunk> Chunks { get; } = Channel.CreateUnbounded<AgentStreamChunk>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }
}

public sealed class CodexAppServerRequestException(
    string message,
    string providerMethod,
    long requestId,
    CodexAppServerMessage response) : InvalidOperationException(message)
{
    public string ProviderMethod { get; } = providerMethod;
    public long RequestId { get; } = requestId;
    public CodexAppServerMessage Response { get; } = response;
}

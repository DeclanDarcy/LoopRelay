using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// m10 process-leak certification (deliverable B): a DIRECT live-process asserter. A single OS codex process is
/// reaped only by <c>AgentProcess.DisposeAsync -&gt; Process.Kill(entireProcessTree)</c>; the per-session boolean
/// the existing tests check never counts LIVE processes across terminal paths. This counter increments on every
/// process construction and decrements on <see cref="CountingAgentProcess.DisposeAsync"/>, so a test can assert
/// <see cref="Live"/> returns to 0 after a failed turn, an external cancel, a faulted prompt, ClosePlanningSession,
/// a Transfer recycle, orchestrator/registry dispose, and the duplicate-open failure window.
/// </summary>
internal sealed class LiveProcessCounter
{
    private int live;
    private int constructed;
    private int killed;

    /// <summary>Net live processes: incremented at construction, decremented at DisposeAsync. 0 == no orphans.</summary>
    public int Live => Volatile.Read(ref live);

    /// <summary>Total processes ever constructed (high-water reach).</summary>
    public int Constructed => Volatile.Read(ref constructed);

    /// <summary>How many processes ran a Kill-equivalent (the entire-process-tree reap on DisposeAsync).</summary>
    public int Killed => Volatile.Read(ref killed);

    public int Increment()
    {
        Interlocked.Increment(ref constructed);
        return Interlocked.Increment(ref live);
    }

    public int Decrement() => Interlocked.Decrement(ref live);

    public void RecordKill() => Interlocked.Increment(ref killed);
}

/// <summary>
/// A held-open process that does the real <see cref="CodexAppServerSession"/> handshake (initialize -&gt; initialized
/// -&gt; thread/start -&gt; turn/start) so the session can actually run/cancel/fail turns, while a shared
/// <see cref="LiveProcessCounter"/> tracks its liveness. DisposeAsync decrements the counter and records the
/// Kill-equivalent (the entire-process-tree reap the real AgentProcess performs). Turn scripting is driven by
/// <see cref="ScriptedAppServerProcess"/>-style reactions, kept minimal: it answers the handshake and emits a
/// completed (or scripted-failed) turn per turn/start.
/// </summary>
internal sealed class CountingAgentProcess : IAgentProcess
{
    private readonly LiveProcessCounter counter;
    private readonly Channel<string> output = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string turnStatus;
    private readonly bool oneShot;
    private int turnCounter;
    private int disposed;

    public CountingAgentProcess(LiveProcessCounter counter, string turnStatus = "completed", bool oneShot = false)
    {
        this.counter = counter;
        this.turnStatus = turnStatus;
        this.oneShot = oneShot;
        counter.Increment();
    }

    public int ProcessId => 7777;

    public AgentProcessState State { get; private set; } = AgentProcessState.Running;

    public int? ExitCode { get; private set; }

    public bool HasExited => State == AgentProcessState.Disposed;

    public Task Completion => completion.Task;

    /// <summary>Completes the output channel mid-turn so a test can model the process dying under an in-flight turn.</summary>
    public void KillOutputStream() => output.Writer.TryComplete();

    public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) =>
        WritePromptAsync(standardInput, cancellationToken);

    public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        // A held-open (app-server) prompt is JSON-RPC framing the React handshake answers; a one-shot prompt is raw
        // text the exec process consumes until stdin EOF (CompleteInputAsync), so only the held-open frames react.
        if (oneShot)
        {
            return Task.CompletedTask;
        }

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            React(line);
        }

        return Task.CompletedTask;
    }

    public Task CompleteInputAsync(CancellationToken cancellationToken = default)
    {
        // A one-shot `codex exec` emits a turn.completed JSONL event then exits at stdin EOF — model that so the
        // one-shot AgentSession's ReadTurnAsync terminates instead of hanging. Held-open processes do nothing here.
        if (oneShot)
        {
            EmitNotification("item/agentMessage/delta", new { delta = "one-shot reply" });
            output.Writer.TryWrite(System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "turn.completed",
                usage = new { input_tokens = 3, output_tokens = 2 },
            }));
            output.Writer.TryComplete();
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ReadOutputLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string line in output.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            State = AgentProcessState.Disposed;
            ExitCode = 0;
            counter.RecordKill();   // the entire-process-tree reap the real AgentProcess.DisposeAsync performs
            counter.Decrement();
            output.Writer.TryComplete();
            completion.TrySetResult();
        }

        return ValueTask.CompletedTask;
    }

    private void React(string line)
    {
        long id = 0;
        string? method;
        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(line);
            System.Text.Json.JsonElement root = document.RootElement;
            method = root.TryGetProperty("method", out System.Text.Json.JsonElement m) &&
                     m.ValueKind == System.Text.Json.JsonValueKind.String
                ? m.GetString()
                : null;
            if (root.TryGetProperty("id", out System.Text.Json.JsonElement i) &&
                i.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                id = i.GetInt64();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        switch (method)
        {
            case "initialize":
                EmitResponse(id, new { userAgent = "x", codexHome = "h", platformFamily = "windows", platformOs = "windows" });
                break;
            case "thread/start":
                EmitResponse(id, new { thread = new { id = "thread-count" } });
                break;
            case "turn/start":
                int index = ++turnCounter;
                EmitResponse(id, new { turn = new { id = $"u{index}", status = "inProgress" } });
                EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = $"reply {index}" });
                EmitNotification("turn/completed", turnStatus == "completed"
                    ? new { turn = new { id = $"u{index}", status = "completed" } }
                    : (object)new { turn = new { id = $"u{index}", status = "failed", error = new { message = "boom" } } });
                break;
        }
    }

    private void EmitResponse(long id, object result) =>
        Emit(new Dictionary<string, object?> { ["id"] = id, ["result"] = result });

    private void EmitNotification(string method, object @params) =>
        Emit(new Dictionary<string, object?> { ["method"] = method, ["params"] = @params });

    private void Emit(object frame) => output.Writer.TryWrite(System.Text.Json.JsonSerializer.Serialize(frame));
}

/// <summary>
/// A launcher that hands back <see cref="CountingAgentProcess"/> instances driven through the shared counter, so a
/// REAL <see cref="AgentRuntime"/> opens/closes REAL <see cref="CodexAppServerSession"/>/<c>AgentSession</c>
/// instances over them — the launcher seam the gap analysis cites
/// (<c>IAgentProcessLauncher</c> / <c>ProcessRunner.StartInteractiveAsync</c>). Every launch is recorded.
/// </summary>
internal sealed class CountingProcessLauncher : IAgentProcessLauncher
{
    private readonly LiveProcessCounter counter;
    private readonly string turnStatus;

    public CountingProcessLauncher(LiveProcessCounter counter, string turnStatus = "completed")
    {
        this.counter = counter;
        this.turnStatus = turnStatus;
    }

    public List<AgentSessionMode> Launches { get; } = new();

    /// <summary>The most recently launched process, so a test can drive process-death / inspect it.</summary>
    public CountingAgentProcess? Last { get; private set; }

    public Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default)
    {
        Launches.Add(mode);
        var process = new CountingAgentProcess(counter, turnStatus, oneShot: mode == AgentSessionMode.OneShot);
        Last = process;
        return Task.FromResult<IAgentProcess>(process);
    }
}

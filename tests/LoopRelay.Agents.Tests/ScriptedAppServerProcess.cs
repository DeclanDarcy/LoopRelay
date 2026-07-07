using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Tests;

/// <summary>
/// An in-memory IAgentProcess that speaks the app-server protocol: it reacts to written JSON-RPC
/// requests by emitting the canned response + notification stream a real codex app-server would.
/// </summary>
internal sealed class ScriptedAppServerProcess : IAgentProcess
{
    private readonly Channel<string> output = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource approvalDeclined = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource approvalAccepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int turnCounter;

    public List<string> Writes { get; } = [];
    public List<string> Methods { get; } = [];
    public string TurnStatus { get; init; } = "completed";

    /// <summary>The error.message a failed turn/completed carries; null omits the error object entirely
    /// (modeling a codex failure that reports no protocol-level message).</summary>
    public string? TurnErrorMessage { get; init; } = "boom";

    /// <summary>The retained stderr tail the session's diagnostics fall back to when the protocol
    /// carried no failure message (IAgentProcess.ErrorSnapshot; interface default is null).</summary>
    public string? ErrorSnapshot { get; init; }

    public bool EmitApprovalRequest { get; init; }

    public string ApprovalCommand { get; init; } = "git push";

    /// <summary>m10 (B): how many item/agentMessage/delta notifications each turn emits (long-output stress).</summary>
    public int DeltaCount { get; init; } = 1;

    /// <summary>m10 (B): when set, the turn emits its deltas then PARKS (never completes) until this task
    /// completes — lets a test cancel the caller's token mid-turn and observe the turn-gate release.</summary>
    public Task? HoldBeforeCompletion { get; set; }

    /// <summary>m10 (B): when true, the process completes its OWN output channel mid-turn (after deltas, before
    /// the turn/completed) — modeling the process dying under an in-flight turn.</summary>
    public bool KillAfterDeltas { get; set; }

    /// <summary>When true, a thread/resume request is answered with a JSON-RPC error (rollout gone).</summary>
    public bool RejectResume { get; init; }

    /// <summary>The threadId carried by the last thread/resume request (null if none arrived).</summary>
    public string? LastResumeThreadId { get; private set; }

    /// <summary>Signaled once a turn has emitted its deltas and is about to park / die (so a test can act).</summary>
    public Task TurnInFlight => turnInFlight.Task;

    private readonly TaskCompletionSource turnInFlight = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ApprovalDeclined => approvalDeclined.Task;

    public Task ApprovalAccepted => approvalAccepted.Task;

    public int ProcessId => 4321;
    public AgentProcessState State { get; private set; } = AgentProcessState.Running;
    public int? ExitCode => null;
    public bool HasExited => State != AgentProcessState.Running;
    public Task Completion => completion.Task;

    public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            lock (Writes)
            {
                Writes.Add(line);
            }

            if (line.Contains("\"decline\"", StringComparison.Ordinal))
            {
                approvalDeclined.TrySetResult();
            }
            else if (line.Contains("\"accept\"", StringComparison.Ordinal))
            {
                approvalAccepted.TrySetResult();
            }

            React(line);
        }

        return Task.CompletedTask;
    }

    public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) =>
        WritePromptAsync(standardInput, cancellationToken);

    public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

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
        State = AgentProcessState.Canceled;
        output.Writer.TryComplete();
        completion.TrySetResult();
        return ValueTask.CompletedTask;
    }

    private void React(string line)
    {
        long id = 0;
        string? method;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            method = root.TryGetProperty("method", out JsonElement m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            if (root.TryGetProperty("id", out JsonElement i) && i.ValueKind == JsonValueKind.Number)
            {
                id = i.GetInt64();
            }
        }
        catch (JsonException)
        {
            return;
        }

        if (method is null)
        {
            return; // a response we sent back (e.g. an approval reply) — nothing to react to
        }

        lock (Methods)
        {
            Methods.Add(method);
        }

        switch (method)
        {
            case "initialize":
                EmitResponse(id, new { userAgent = "x", codexHome = "h", platformFamily = "windows", platformOs = "windows" });
                break;

            case "thread/start":
                EmitResponse(id, new { thread = new { id = "thread-xyz" } });
                break;

            case "thread/resume":
                string? requested = null;
                try
                {
                    using (JsonDocument resumeDoc = JsonDocument.Parse(line))
                    {
                        requested = resumeDoc.RootElement.GetProperty("params").GetProperty("threadId").GetString();
                    }
                }
                catch (Exception)
                {
                    // Malformed frame — leave requested null; the assertion in the test will surface it.
                }

                LastResumeThreadId = requested;
                if (RejectResume)
                {
                    EmitError(id, $"no rollout found for thread {requested}");
                }
                else
                {
                    EmitResponse(id, new { thread = new { id = requested } });
                }

                break;

            case "turn/start":
                int index = ++turnCounter;
                EmitResponse(id, new { turn = new { id = $"u{index}", status = "inProgress" } });
                if (EmitApprovalRequest)
                {
                    EmitServerRequest("appr-1", "item/commandExecution/requestApproval", new { itemId = "i1", command = ApprovalCommand });
                }

                EmitNotification("turn/started", new { threadId = "thread-xyz", turn = new { id = $"u{index}", status = "inProgress" } });

                // m10 (B) long-output: emit DeltaCount deltas in order (default 1). The single-delta default keeps
                // the m4 reply "reply N" verbatim so existing tests are byte-unchanged.
                if (DeltaCount <= 1)
                {
                    EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = $"reply {index}" });
                }
                else
                {
                    for (int d = 0; d < DeltaCount; d++)
                    {
                        EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = $"d{d}|" });
                    }
                }

                EmitNotification("thread/tokenUsage/updated", new { tokenUsage = new { last = new { inputTokens = 11, outputTokens = 5 } } });

                // m10 (B) cancel-mid-turn / process-death-mid-turn: optionally signal in-flight, then either PARK
                // (never completing the turn — the caller cancels), KILL the output stream (process death), or
                // emit the terminal turn/completed as normal.
                turnInFlight.TrySetResult();
                if (KillAfterDeltas)
                {
                    output.Writer.TryComplete();
                    break;
                }

                if (HoldBeforeCompletion is not null)
                {
                    break; // never emit turn/completed; the caller's cancellation is what ends the turn
                }

                EmitNotification("turn/completed", TurnStatus == "completed"
                    ? new { turn = new { id = $"u{index}", status = "completed" } }
                    : TurnErrorMessage is { } errorMessage
                        ? (object)new { turn = new { id = $"u{index}", status = "failed", error = new { message = errorMessage } } }
                        : new { turn = new { id = $"u{index}", status = "failed" } });
                break;
        }
    }

    private void EmitResponse(long id, object result) =>
        Emit(new Dictionary<string, object?> { ["id"] = id, ["result"] = result });

    private void EmitError(long id, string message) =>
        Emit(new Dictionary<string, object?> { ["id"] = id, ["error"] = new { message } });

    private void EmitNotification(string method, object @params) =>
        Emit(new Dictionary<string, object?> { ["method"] = method, ["params"] = @params });

    private void EmitServerRequest(string requestId, string method, object @params) =>
        Emit(new Dictionary<string, object?> { ["id"] = requestId, ["method"] = method, ["params"] = @params });

    private void Emit(object frame) => output.Writer.TryWrite(JsonSerializer.Serialize(frame));
}

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

public sealed class CodexAppServerSessionTests
{
    private static AgentSessionSpec Spec() => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.OperationalExecution,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.Medium),
        workingDirectory: "/repo");

    private static CodexAppServerSession NewSession(ScriptedAppServerProcess process) =>
        new(Spec(), process, new DeterministicAgentTokenEstimator());

    [Fact]
    public async Task SingleTurnRunsHandshakeAndReturnsReplyWithReportedUsage()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("reply 1", result.Output);
        Assert.Equal(11, result.Usage.PromptTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
        Assert.Equal(1, session.CompletedTurns);

        // Handshake ran once, initialize first, then initialized, thread/start, turn/start.
        Assert.Equal("initialize", process.Methods[0]);
        Assert.Equal(["initialize", "initialized", "thread/start", "turn/start"], process.Methods);
    }

    [Fact]
    public async Task SecondTurnReusesThreadWithoutReinitializing()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult first = await session.RunTurnAsync("one");
        AgentTurnResult second = await session.RunTurnAsync("two");

        Assert.Equal("reply 1", first.Output);
        Assert.Equal("reply 2", second.Output);
        Assert.Equal(2, session.CompletedTurns);
        Assert.Equal(1, process.Methods.Count(method => method == "initialize"));
        Assert.Equal(1, process.Methods.Count(method => method == "thread/start"));
        Assert.Equal(2, process.Methods.Count(method => method == "turn/start"));
    }

    [Fact]
    public async Task FailedTurnSurfacesFailedState()
    {
        var process = new ScriptedAppServerProcess { TurnStatus = "failed" };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Failed, result.State);
    }

    [Fact]
    public async Task DeltasAreStreamedToOnChunk()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        var chunks = new List<string>();
        await session.RunTurnAsync("hello", chunk =>
        {
            lock (chunks)
            {
                chunks.Add(chunk.Content);
            }

            return Task.CompletedTask;
        });

        Assert.Contains("reply 1", chunks);
    }

    [Fact]
    public async Task ApprovalRequestsAreAutoDeclinedAndDoNotBlockTheTurn()
    {
        var process = new ScriptedAppServerProcess { EmitApprovalRequest = true };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");
        // The decline is written by the background writer task, so await its observable signal.
        await process.ApprovalDeclined.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Contains(process.Writes, write => write.Contains("\"decline\""));
    }

    [Fact]
    public async Task DisposedSessionRejectsNewTurns()
    {
        var process = new ScriptedAppServerProcess();
        CodexAppServerSession session = NewSession(process);
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.RunTurnAsync("hello"));
    }

    /// <summary>
    /// An in-memory IAgentProcess that speaks the app-server protocol: it reacts to written JSON-RPC
    /// requests by emitting the canned response + notification stream a real codex app-server would.
    /// </summary>
    private sealed class ScriptedAppServerProcess : IAgentProcess
    {
        private readonly Channel<string> output = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource approvalDeclined = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int turnCounter;

        public List<string> Writes { get; } = [];
        public List<string> Methods { get; } = [];
        public string TurnStatus { get; init; } = "completed";
        public bool EmitApprovalRequest { get; init; }

        public Task ApprovalDeclined => approvalDeclined.Task;

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

                case "turn/start":
                    int index = ++turnCounter;
                    string reply = $"reply {index}";
                    EmitResponse(id, new { turn = new { id = $"u{index}", status = "inProgress" } });
                    if (EmitApprovalRequest)
                    {
                        EmitServerRequest("appr-1", "item/commandExecution/requestApproval", new { itemId = "i1" });
                    }

                    EmitNotification("turn/started", new { threadId = "thread-xyz", turn = new { id = $"u{index}", status = "inProgress" } });
                    EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = reply });
                    EmitNotification("thread/tokenUsage/updated", new { tokenUsage = new { last = new { inputTokens = 11, outputTokens = 5 } } });
                    EmitNotification("turn/completed", TurnStatus == "completed"
                        ? new { turn = new { id = $"u{index}", status = "completed" } }
                        : (object)new { turn = new { id = $"u{index}", status = "failed", error = new { message = "boom" } } });
                    break;
            }
        }

        private void EmitResponse(long id, object result) =>
            Emit(new Dictionary<string, object?> { ["id"] = id, ["result"] = result });

        private void EmitNotification(string method, object @params) =>
            Emit(new Dictionary<string, object?> { ["method"] = method, ["params"] = @params });

        private void EmitServerRequest(string requestId, string method, object @params) =>
            Emit(new Dictionary<string, object?> { ["id"] = requestId, ["method"] = method, ["params"] = @params });

        private void Emit(object frame) => output.Writer.TryWrite(JsonSerializer.Serialize(frame));
    }
}

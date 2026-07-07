using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Services;

namespace LoopRelay.Agents.Tests;

public sealed class AgentSessionTests
{
    private static AgentSessionSpec Spec() =>
        new(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.Planning,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High),
            workingDirectory: "/repo");

    [Fact]
    public async Task PersistentSessionRunsMultipleTurnsInOneProcessWithoutClosingStdin()
    {
        var process = new FakeInteractiveAgentProcess();
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.Persistent,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        var chunks1 = new List<string>();
        Task<AgentTurnResult> turn1 = session.RunTurnAsync(
            "first prompt",
            chunk => { chunks1.Add(chunk.Content); return Task.CompletedTask; });
        process.Emit("alpha");
        process.Emit("beta");
        process.Emit(SentinelTurnBoundaryDetector.DefaultSentinel);
        AgentTurnResult result1 = await turn1;

        var chunks2 = new List<string>();
        Task<AgentTurnResult> turn2 = session.RunTurnAsync(
            "second prompt",
            chunk => { chunks2.Add(chunk.Content); return Task.CompletedTask; });
        process.Emit("gamma");
        process.Emit(SentinelTurnBoundaryDetector.DefaultSentinel);
        AgentTurnResult result2 = await turn2;

        Assert.Equal(AgentTurnState.Completed, result1.State);
        Assert.Equal(1, result1.TurnIndex);
        Assert.Equal("alpha\nbeta", result1.Output);
        Assert.Equal(["alpha", "beta"], chunks1.ToArray());

        Assert.Equal(AgentTurnState.Completed, result2.State);
        Assert.Equal(2, result2.TurnIndex);
        Assert.Equal("gamma", result2.Output);
        Assert.Equal(["gamma"], chunks2.ToArray());

        Assert.Equal(2, session.CompletedTurns);
        Assert.Equal(2, process.PromptWrites.Count);
        Assert.False(process.InputCompleted);
        Assert.True(session.TotalUsage.TotalTokens > 0);
    }

    [Fact]
    public async Task OneShotSessionCompletesOnStreamEndAndClosesStdin()
    {
        var process = new FakeInteractiveAgentProcess();
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.OneShot,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        Task<AgentTurnResult> turn = session.RunTurnAsync("only prompt");
        process.Emit("one");
        process.Emit("two");
        process.EndOutput();
        AgentTurnResult result = await turn;

        // A null exit code (a fake/unknown process) keeps the legacy semantics: stream end completes the turn.
        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("one\ntwo", result.Output);
        Assert.Null(result.Diagnostics);
        Assert.True(process.InputCompleted);
    }

    // Regression for the silent one-shot failure: codex refusing to run at all (e.g. "Not inside a
    // trusted directory", exit 1, no boundary event) used to map stream end to Completed because the
    // exit code was never consulted. A nonzero exit now FAILS the turn and carries the retained
    // stderr tail as diagnostics so the operator sees why.
    [Fact]
    public async Task OneShotStreamEndWithNonZeroExitFailsAndSurfacesStderrTail()
    {
        var process = new FakeInteractiveAgentProcess
        {
            ScriptedExitCode = 1,
            ScriptedErrorSnapshot = "Not inside a trusted directory and --skip-git-repo-check was not specified."
        };
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.OneShot,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        Task<AgentTurnResult> turn = session.RunTurnAsync("only prompt");
        process.EndOutput();
        AgentTurnResult result = await turn;

        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Contains("Not inside a trusted directory", result.Diagnostics);
    }

    [Fact]
    public async Task OneShotStreamEndWithZeroExitCompletesWithoutDiagnostics()
    {
        var process = new FakeInteractiveAgentProcess { ScriptedExitCode = 0 };
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.OneShot,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        Task<AgentTurnResult> turn = session.RunTurnAsync("only prompt");
        process.Emit("done");
        process.EndOutput();
        AgentTurnResult result = await turn;

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("done", result.Output);
        Assert.Null(result.Diagnostics);
    }

    [Fact]
    public async Task OneShotExecJsonToolItemsStreamOnceWithoutPollutingOutput()
    {
        var process = new FakeInteractiveAgentProcess();
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.OneShot,
            process,
            new CodexEventTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        var chunks = new List<AgentStreamChunk>();
        Task<AgentTurnResult> turn = session.RunTurnAsync("only prompt", chunk =>
        {
            chunks.Add(chunk);
            return Task.CompletedTask;
        });

        process.Emit("""{"type":"item.started","item":{"id":"c1","type":"command_execution","command":"git status"}}""");
        process.Emit("""{"type":"item.completed","item":{"id":"c1","type":"command_execution","command":"git status","exitCode":0}}""");
        process.Emit("""{"type":"item.completed","item":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"done"}]}}""");
        process.Emit("""{"type":"turn.completed","usage":{"input_tokens":1,"output_tokens":1}}""");

        AgentTurnResult result = await turn;

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("done", result.Output);
        Assert.Collection(
            chunks,
            chunk =>
            {
                Assert.Equal(AgentStreamChunkKind.ToolCall, chunk.Kind);
                Assert.Equal("$ git status", chunk.Content);
            },
            chunk =>
            {
                Assert.Equal(AgentStreamChunkKind.AgentMessage, chunk.Kind);
                Assert.Equal("done", chunk.Content);
            });
    }

    [Fact]
    public async Task DisposeClosesStdinAndDisposesProcessForPersistentSession()
    {
        var process = new FakeInteractiveAgentProcess();
        var session = new AgentSession(
            Spec(),
            AgentSessionMode.Persistent,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        await session.DisposeAsync();

        Assert.True(process.WasDisposed);
        Assert.True(process.InputCompleted);
    }

    [Fact]
    public async Task CancelDisposesUnderlyingProcess()
    {
        var process = new FakeInteractiveAgentProcess();
        await using var session = new AgentSession(
            Spec(),
            AgentSessionMode.Persistent,
            process,
            new SentinelTurnBoundaryDetector(),
            new DeterministicAgentTokenEstimator());

        await session.CancelAsync();

        Assert.True(process.WasDisposed);
    }

    private sealed class FakeInteractiveAgentProcess : IAgentProcess
    {
        private readonly Channel<string> output = Channel.CreateUnbounded<string>();
        private readonly TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> PromptWrites { get; } = [];

        public bool InputCompleted { get; private set; }

        public bool WasDisposed { get; private set; }

        public int ProcessId => 4242;

        public AgentProcessState State { get; private set; } = AgentProcessState.Running;

        /// <summary>Exit code the fake reports once set (null = a process that cannot report one).</summary>
        public int? ScriptedExitCode { get; init; }

        /// <summary>Stderr tail the fake reports (null = nothing captured).</summary>
        public string? ScriptedErrorSnapshot { get; init; }

        public int? ExitCode => ScriptedExitCode;

        public string? ErrorSnapshot => ScriptedErrorSnapshot;

        public bool HasExited => State is AgentProcessState.Exited or AgentProcessState.Canceled;

        public Task Completion => completion.Task;

        public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
        {
            PromptWrites.Add(text);
            return Task.CompletedTask;
        }

        public Task CompleteInputAsync(CancellationToken cancellationToken = default)
        {
            InputCompleted = true;
            return Task.CompletedTask;
        }

        public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default)
        {
            PromptWrites.Add(standardInput);
            InputCompleted = true;
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

        public void Emit(string line) => output.Writer.TryWrite(line);

        public void EndOutput() => output.Writer.TryComplete();

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            State = AgentProcessState.Canceled;
            output.Writer.TryComplete();
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}

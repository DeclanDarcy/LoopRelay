using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

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

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("one\ntwo", result.Output);
        Assert.True(process.InputCompleted);
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

        public int? ExitCode { get; private set; }

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

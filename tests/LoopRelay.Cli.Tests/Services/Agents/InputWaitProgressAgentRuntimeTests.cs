using System.Collections.Concurrent;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Primitives.Streams;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Tests.Services.Support;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Agents;

public class InputWaitProgressAgentRuntimeTests
{
    private static AgentSessionSpec Spec() =>
        AgentSpecs.Decision(
            new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" },
            TestAgentConfiguration.Brain);

    [Fact]
    public async Task OneShot_ShowsEstimatedPromptTokensWithoutEtaPercentOrCacheClaims()
    {
        var console = new RecordingLoopConsole();
        var sink = new RecordingInputWaitSink();
        var inner = new ScriptedRuntime(async onChunk =>
        {
            await onChunk(new AgentStreamChunk(1, AgentProcessOutputStream.StandardOutput, "hello"));
            return new AgentTurnResult(1, AgentTurnState.Completed, "hello", AgentTokenUsage.Zero);
        });
        var runtime = new InputWaitProgressAgentRuntime(
            inner,
            new DeterministicAgentTokenEstimator(),
            new ConsoleInputWaitProgressRenderer(console),
            sink);

        await runtime.RunOneShotAsync(Spec(), "12345");

        Assert.Contains(console.Events, e => e.Kind == "progress" && e.Text.Contains("promptTokensEstimated=2"));
        IReadOnlyList<string> progressText = console.Events
            .Where(e => e.Kind.StartsWith("progress", StringComparison.Ordinal))
            .Select(e => e.Text)
            .ToList();
        Assert.DoesNotContain(progressText, text => text.Contains('%'));
        Assert.DoesNotContain(progressText, text => text.Contains("ETA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(progressText, text => text.Contains("cache", StringComparison.OrdinalIgnoreCase));

        InputWaitObservation observation = Assert.Single(sink.Observations);
        Assert.Equal(2, observation.PromptTokensEstimated);
        Assert.Equal("DeterministicAgentTokenEstimator", observation.TokenEstimateSource);
        Assert.NotNull(observation.FirstProtocolEventAt);
        Assert.NotNull(observation.FirstOutputAt);
    }

    [Fact]
    public async Task ToolChunksRecordProtocolActivityAndFirstVisibleOutput()
    {
        var sink = new RecordingInputWaitSink();
        var inner = new ScriptedRuntime(async onChunk =>
        {
            await onChunk(new AgentStreamChunk(
                1,
                AgentProcessOutputStream.StandardOutput,
                "$ dotnet build",
                AgentStreamChunkKind.ToolCall));
            return new AgentTurnResult(1, AgentTurnState.Completed, string.Empty, AgentTokenUsage.Zero);
        });
        var runtime = new InputWaitProgressAgentRuntime(
            inner,
            new DeterministicAgentTokenEstimator(),
            new ConsoleInputWaitProgressRenderer(new RecordingLoopConsole()),
            sink);

        await runtime.RunOneShotAsync(Spec(), "prompt");

        InputWaitObservation observation = Assert.Single(sink.Observations);
        Assert.NotNull(observation.FirstProtocolEventAt);
        Assert.NotNull(observation.FirstOutputAt);
    }

    [Fact]
    public async Task ToolChunksStopInputWaitProgress()
    {
        var renderer = new RecordingInputWaitProgressRenderer(TimeSpan.FromMilliseconds(1));
        var inner = new ScriptedRuntime(async onChunk =>
        {
            // Wait for a real waiting tick rather than sleeping around one: this proves the progress loop
            // is live before the tool chunk lands, so "no waiting after first-output" cannot pass vacuously.
            await renderer.FirstWaiting.WaitAsync(TimeSpan.FromSeconds(60));
            await onChunk(new AgentStreamChunk(
                1,
                AgentProcessOutputStream.StandardOutput,
                "$ dotnet build",
                AgentStreamChunkKind.ToolCall));
            return new AgentTurnResult(1, AgentTurnState.Completed, string.Empty, AgentTokenUsage.Zero);
        });
        var runtime = new InputWaitProgressAgentRuntime(
            inner,
            new DeterministicAgentTokenEstimator(),
            renderer);

        // Turn completion joins the render loop before returning, so every render event this turn can
        // produce is already recorded by the time these run — no settling delay is needed.
        await runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Contains("waiting", renderer.Events);
        Assert.Contains("first-output", renderer.Events);
        Assert.DoesNotContain("completed-without-output", renderer.Events);
        Assert.DoesNotContain(
            renderer.Events.SkipWhile(e => e != "first-output").Skip(1),
            e => e == "waiting");
    }

    [Fact]
    public async Task WaitingRenderCannotLandAfterFirstOutput()
    {
        var renderer = new BlockingWaitingInputWaitProgressRenderer();
        var inner = new ScriptedRuntime(async onChunk =>
        {
            await renderer.WaitingEntered.WaitAsync(TimeSpan.FromSeconds(60));

            // Deliver the chunk on a dedicated thread and release the suspended waiting render once that
            // thread is observed blocked, which is the state it reaches when it parks on the tracker's
            // lock that the render loop holds while sitting inside Waiting. This forces the contention a
            // fixed delay could only hope to hit; if the chunk never blocks, the thread simply ends and
            // the spin exits at once, which is an accepted outcome.
            //
            // Caveat, so the guarantee is not overstated: ThreadState.WaitSleepJoin is set by ANY blocking
            // wait, so observing it does not prove this thread reached the tracker lock specifically -- the
            // spin can exit early on an unrelated block. Thread.ThreadState is a debugging aid, not a
            // synchronization primitive. This is still strictly better than the fixed 25 ms delay it
            // replaced (which could elapse while testing nothing), but it is a best-effort interleave.
            Exception? chunkFailure = null;
            var chunkThread = new Thread(() =>
            {
                try
                {
                    onChunk(new AgentStreamChunk(
                        1,
                        AgentProcessOutputStream.StandardOutput,
                        "hello",
                        AgentStreamChunkKind.AgentMessage)).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    chunkFailure = exception;
                }
            }) { IsBackground = true };
            chunkThread.Start();
            Assert.True(
                SpinWait.SpinUntil(
                    () => !chunkThread.IsAlive
                        || (chunkThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(60)),
                "the chunk delivery thread neither blocked nor finished");

            renderer.ReleaseWaiting();
            Assert.True(chunkThread.Join(TimeSpan.FromSeconds(60)), "the chunk delivery never completed");
            if (chunkFailure is not null)
            {
                throw chunkFailure;
            }

            return new AgentTurnResult(1, AgentTurnState.Completed, "hello", AgentTokenUsage.Zero);
        });
        var runtime = new InputWaitProgressAgentRuntime(
            inner,
            new DeterministicAgentTokenEstimator(),
            renderer);

        await runtime.RunOneShotAsync(Spec(), "prompt");

        string[] events = renderer.Events.ToArray();
        int waitingIndex = Array.IndexOf(events, "waiting");
        int firstOutputIndex = Array.IndexOf(events, "first-output");
        Assert.NotEqual(-1, waitingIndex);
        Assert.NotEqual(-1, firstOutputIndex);
        Assert.True(waitingIndex < firstOutputIndex, string.Join(", ", events));
    }

    [Fact]
    public async Task ObservationSinkFailureDoesNotBreakTheTurn()
    {
        var inner = new ScriptedRuntime(_ =>
            Task.FromResult(new AgentTurnResult(1, AgentTurnState.Completed, "ok", AgentTokenUsage.Zero)));
        var runtime = new InputWaitProgressAgentRuntime(
            inner,
            new DeterministicAgentTokenEstimator(),
            new ConsoleInputWaitProgressRenderer(new RecordingLoopConsole()),
            new ThrowingInputWaitSink());

        AgentTurnResult result = await runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Equal("ok", result.Output);
    }

    private sealed class ScriptedRuntime(
        Func<Func<AgentStreamChunk, Task>, Task<AgentTurnResult>> turn) : IAgentRuntime
    {
        public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAgentSession>(new ScriptedSession(this, spec));

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            turn(onChunk ?? (_ => Task.CompletedTask));

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;

        private sealed class ScriptedSession(ScriptedRuntime runtime, AgentSessionSpec spec) : IAgentSession
        {
            public SessionIdentity SessionId => spec.SessionId;
            public string RepositoryId => spec.RepositoryId;
            public SessionRole Role => spec.Role;
            public AgentSessionMode Mode => AgentSessionMode.Persistent;
            public AgentProcessState State => AgentProcessState.Running;
            public int CompletedTurns => 0;
            public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
            public string? ThreadId => "thread";

            public Task<AgentTurnResult> RunTurnAsync(
                string prompt,
                Func<AgentStreamChunk, Task>? onChunk = null,
                CancellationToken cancellationToken = default) =>
                runtime.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);

            public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingInputWaitSink : IInputWaitObservationSink
    {
        public List<InputWaitObservation> Observations { get; } = [];

        public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingInputWaitSink : IInputWaitObservationSink
    {
        public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken) =>
            throw new IOException("disk full");
    }

    private sealed class RecordingInputWaitProgressRenderer(TimeSpan refreshInterval) : IInputWaitProgressRenderer
    {
        private readonly ConcurrentQueue<string> events = new();
        private readonly TaskCompletionSource firstWaiting =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan RefreshInterval => refreshInterval;

        /// <summary>Completes on the first waiting tick, so a test can sequence against a live progress
        /// loop instead of sleeping long enough to assume one.</summary>
        public Task FirstWaiting => firstWaiting.Task;

        public IReadOnlyList<string> Events => events.ToArray();

        public void Started(InputWaitProgressSnapshot snapshot) => events.Enqueue("started");

        public void Waiting(InputWaitProgressSnapshot snapshot)
        {
            events.Enqueue("waiting");
            firstWaiting.TrySetResult();
        }

        public void FirstOutput(InputWaitProgressSnapshot snapshot) => events.Enqueue("first-output");

        public void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot) =>
            events.Enqueue("completed-without-output");
    }

    private sealed class BlockingWaitingInputWaitProgressRenderer : IInputWaitProgressRenderer
    {
        private readonly ConcurrentQueue<string> events = new();
        private readonly TaskCompletionSource waitingEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseWaiting =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(1);

        public Task WaitingEntered => waitingEntered.Task;

        public IReadOnlyList<string> Events => events.ToArray();

        public void ReleaseWaiting() => releaseWaiting.TrySetResult();

        public void Started(InputWaitProgressSnapshot snapshot) => events.Enqueue("started");

        public void Waiting(InputWaitProgressSnapshot snapshot)
        {
            waitingEntered.TrySetResult();
            releaseWaiting.Task.GetAwaiter().GetResult();
            events.Enqueue("waiting");
        }

        public void FirstOutput(InputWaitProgressSnapshot snapshot) => events.Enqueue("first-output");

        public void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot) =>
            events.Enqueue("completed-without-output");
    }
}

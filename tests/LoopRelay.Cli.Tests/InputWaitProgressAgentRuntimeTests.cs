using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Services;
using LoopRelay.Cli;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Diagnostics;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class InputWaitProgressAgentRuntimeTests
{
    private static AgentSessionSpec Spec() =>
        Cli.AgentSpecs.Decision(new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" });

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
    public async Task ToolChunksRecordProtocolActivityWithoutFirstVisibleOutput()
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
        Assert.Null(observation.FirstOutputAt);
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
}

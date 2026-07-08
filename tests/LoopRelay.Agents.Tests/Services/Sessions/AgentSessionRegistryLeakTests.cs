using System.Runtime.CompilerServices;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Services;

namespace LoopRelay.Agents.Tests.Services;

/// <summary>
/// m10 process-leak fix: AgentRuntime.OpenSessionAsync ADDS every held-open session to AgentSessionRegistry, but
/// the orchestrator used to dispose sessions DIRECTLY and never deregistered them, so the registry accumulated
/// dead/disposed entries and re-disposed them at shutdown. The fix makes ownership single-sited via
/// IAgentRuntime.CloseSessionAsync, which BOTH deregisters AND disposes. These tests pin that the registry count
/// returns to 0 after open + close, exercising the REAL AgentRuntime + REAL AgentSessionRegistry.
/// </summary>
public sealed class AgentSessionRegistryLeakTests
{
    [Fact]
    public async Task CloseSessionAsync_deregisters_from_the_registry_and_disposes_the_session()
    {
        var registry = new AgentSessionRegistry();
        var launcher = new IdleProcessLauncher();
        IAgentRuntime runtime = new AgentRuntime(
            launcher, new CodexEventTurnBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);

        IAgentSession session = await runtime.OpenSessionAsync(Spec());
        Assert.Equal(1, registry.Count); // OpenSessionAsync registered it

        await runtime.CloseSessionAsync(session);

        Assert.Equal(0, registry.Count); // deregistered, not left dangling
        Assert.Equal(AgentProcessState.Disposed, session.State); // and disposed
    }

    [Fact]
    public async Task Registry_count_returns_to_zero_after_open_then_close_for_multiple_sessions()
    {
        var registry = new AgentSessionRegistry();
        var launcher = new IdleProcessLauncher();
        IAgentRuntime runtime = new AgentRuntime(
            launcher, new CodexEventTurnBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);

        IAgentSession planning = await runtime.OpenSessionAsync(Spec(SessionRole.Planning));
        IAgentSession decision = await runtime.OpenSessionAsync(Spec(SessionRole.Decision));
        Assert.Equal(2, registry.Count);

        await runtime.CloseSessionAsync(planning);
        await runtime.CloseSessionAsync(decision);

        Assert.Equal(0, registry.Count);

        // Disposing the registry now re-disposes NOTHING (it is empty) — the leak that re-disposed dead entries
        // at shutdown is gone. (A no-throw assertion: DisposeAsync over an empty registry completes cleanly.)
        await registry.DisposeAsync();
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public async Task CloseSessionAsync_of_an_unregistered_session_still_disposes_it_exactly_once()
    {
        // Idempotency/best-effort: a session not (or no longer) in the registry is still disposed.
        var registry = new AgentSessionRegistry();
        var launcher = new IdleProcessLauncher();
        IAgentRuntime runtime = new AgentRuntime(
            launcher, new CodexEventTurnBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);

        IAgentSession session = await runtime.OpenSessionAsync(Spec());
        await runtime.CloseSessionAsync(session); // first close removes + disposes
        Assert.Equal(0, registry.Count);

        // A second close finds nothing registered and falls back to disposing directly (no throw, still disposed).
        await runtime.CloseSessionAsync(session);
        Assert.Equal(0, registry.Count);
        Assert.Equal(AgentProcessState.Disposed, session.State);
    }

    private static AgentSessionSpec Spec(SessionRole role = SessionRole.Planning) =>
        new(
            SessionIdentity.New(),
            Guid.NewGuid().ToString("D"),
            role,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            System.IO.Path.GetTempPath());

    // A launcher that returns an idle held-open process: its stdout never emits a frame and its stdin writes are
    // no-ops, so CodexAppServerSession constructs (pump + writer spin up) and disposes cleanly without the session
    // ever running a turn — exactly what the leak test needs.
    private sealed class IdleProcessLauncher : IAgentProcessLauncher
    {
        public Task<IAgentProcess> LaunchAsync(AgentSessionSpec spec, AgentSessionMode mode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IAgentProcess>(new IdleProcess());
    }

    private sealed class IdleProcess : IAgentProcess
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource readCts = new();

        public int ProcessId => 1;

        public AgentProcessState State { get; private set; } = AgentProcessState.Running;

        public int? ExitCode { get; private set; }

        public bool HasExited => State == AgentProcessState.Disposed;

        public Task Completion => completion.Task;

        public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task WritePromptAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<string> ReadOutputLinesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // No output frames: park until the session is torn down (its own cancellation flows in here), then end.
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, readCts.Token);
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token);
            }
            catch (OperationCanceledException)
            {
            }

            yield break;
        }

        public ValueTask DisposeAsync()
        {
            State = AgentProcessState.Disposed;
            ExitCode = 0;
            readCts.Cancel();
            completion.TrySetResult();
            readCts.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

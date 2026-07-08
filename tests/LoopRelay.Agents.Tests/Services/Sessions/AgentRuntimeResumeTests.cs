using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Services;

namespace LoopRelay.Agents.Tests.Services;

public sealed class AgentRuntimeResumeTests
{
    private sealed class StubLauncher(IAgentProcess process) : IAgentProcessLauncher
    {
        public Task<IAgentProcess> LaunchAsync(
            AgentSessionSpec spec, AgentSessionMode mode, CancellationToken cancellationToken = default) =>
            Task.FromResult(process);
    }

    // OpenSessionAsync never inspects turn boundaries (that is the one-shot path), so this must never be hit.
    private sealed class UnusedBoundaryDetector : IAgentTurnBoundaryDetector
    {
        public AgentLineInspection Inspect(string line) => throw new NotSupportedException();
    }

    private static AgentSessionSpec ResumeSpec() => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.Decision,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
        workingDirectory: "/repo",
        resumeThreadId: "thread-old");

    private static AgentRuntime NewRuntime(ScriptedAppServerProcess process, AgentSessionRegistry registry) =>
        new(new StubLauncher(process), new UnusedBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);

    [Fact]
    public async Task OpenSessionWithResumeIdRunsTheHandshakeEagerly()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        IAgentSession session = await runtime.OpenSessionAsync(ResumeSpec());

        // The handshake already ran at open time — before any turn.
        Assert.Equal(["initialize", "initialized", "thread/resume"], process.Methods);
        Assert.Equal("thread-old", session.ThreadId);
        Assert.Equal(1, registry.Count);

        await runtime.CloseSessionAsync(session);
    }

    [Fact]
    public async Task FailedResumeDisposesTheProcessDeregistersAndThrowsTheTypedException()
    {
        var process = new ScriptedAppServerProcess { RejectResume = true };
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        await Assert.ThrowsAsync<AgentSessionResumeException>(() => runtime.OpenSessionAsync(ResumeSpec()));

        Assert.True(process.HasExited);   // the codex process was torn down, not leaked
        Assert.Equal(0, registry.Count);  // and no stale registry entry survives
    }

    [Fact]
    public async Task OpenSessionWithoutResumeIdStaysLazy()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            workingDirectory: "/repo");

        IAgentSession session = await runtime.OpenSessionAsync(spec);

        Assert.Empty(process.Methods);   // no frame sent — the handshake waits for the first turn
        Assert.Null(session.ThreadId);

        await runtime.CloseSessionAsync(session);
    }
}

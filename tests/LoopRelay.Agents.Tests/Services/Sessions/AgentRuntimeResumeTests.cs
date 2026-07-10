using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Agents.Tests.Services.Process;
using System.Text.Json;

namespace LoopRelay.Agents.Tests.Services.Sessions;

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

    private static SessionContinuityProfile Profile(
        SessionOperationSupport resume = SessionOperationSupport.Supported,
        SessionOperationSupport excludeTurns = SessionOperationSupport.Supported,
        SessionOperationSupport read = SessionOperationSupport.Unknown,
        SessionOperationSupport write = SessionOperationSupport.Unknown,
        SessionOperationSupport fork = SessionOperationSupport.Unknown) =>
        new(
            "codex", "test", "0.142.5", "codex", "app-server-v2", "schema",
            new Dictionary<string, bool> { ["experimentalApi"] = true },
            new Dictionary<string, string>(),
            new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
            {
                [SessionContinuityOperation.Resume] = new SessionOperationSupportDescriptor(
                    resume,
                    "v2",
                    new Dictionary<string, SessionParameterSupport>
                    {
                        [SessionContinuityProfile.ExcludeTurnsParameter] = new(excludeTurns, "test"),
                    },
                    "load", "same-id", "none", "exact-id", "test"),
                [SessionContinuityOperation.ConversationRead] = new SessionOperationSupportDescriptor(
                    read, "v2", new Dictionary<string, SessionParameterSupport>(),
                    "read", "same-id", "none", "exact-id", "test"),
                [SessionContinuityOperation.ConversationWrite] = new SessionOperationSupportDescriptor(
                    write, "v2", new Dictionary<string, SessionParameterSupport>(),
                    "turn", "same-id", "marker", "exact-id", "test"),
                [SessionContinuityOperation.Fork] = new SessionOperationSupportDescriptor(
                    fork, "v2", new Dictionary<string, SessionParameterSupport>(),
                    "clone", "stable-parent-child", "none", "enumerate-children", "test"),
            },
            256_000, "test", "test", negotiatedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task ResumeSessionRunsTheHandshakeEagerlyAndReturnsTheSameId()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        SessionResumeResult result = await runtime.ResumeSessionAsync(new SessionResumeRequest(
            ResumeSpec(), new ProviderSessionReference("codex", "thread-old"), Profile()));

        // The handshake already ran at open time — before any turn.
        Assert.Equal(["initialize", "initialized", "thread/resume"], process.Methods);
        Assert.Equal(SessionResumeOutcome.SuccessfulResume, result.Outcome);
        Assert.Equal("thread-old", result.Session!.ThreadId);
        Assert.Equal("thread-old", result.Resolved!.ThreadId);
        Assert.True(process.LastInitializeExperimentalApi);
        Assert.Equal(1, registry.Count);

        await runtime.CloseSessionAsync(result.Session);
    }

    [Fact]
    public async Task StructuredInvalidParamsIsDeterministicAndDisposesTheFailedProcess()
    {
        var process = new ScriptedAppServerProcess { RejectResume = true };
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        SessionResumeResult result = await runtime.ResumeSessionAsync(new SessionResumeRequest(
            ResumeSpec(), new ProviderSessionReference("codex", "thread-old"), Profile()));

        Assert.Equal(SessionResumeOutcome.DeterministicProtocolFailure, result.Outcome);
        Assert.Equal(-32602, result.Failure!.JsonRpcCode);
        Assert.Equal("excludeTurns", result.Failure.ErrorData.GetProperty("field").GetString());
        Assert.False(result.Transport.TurnSubmitted);
        Assert.True(process.HasExited);   // the codex process was torn down, not leaked
        Assert.Equal(0, registry.Count);  // and no stale registry entry survives
    }

    [Fact]
    public async Task AmbiguousProviderErrorIsUnknownRatherThanUnavailable()
    {
        string home = Directory.CreateTempSubdirectory("codex-resume-evidence-").FullName;
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"),
            JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = "thread-old" } }));
        var process = new ScriptedAppServerProcess
        {
            RejectResume = true,
            ResumeErrorCode = -32000,
            CodexHome = home,
        };
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        SessionResumeResult result = await runtime.ResumeSessionAsync(new SessionResumeRequest(
            ResumeSpec(), new ProviderSessionReference("codex", "thread-old"), Profile()));

        Assert.Equal(SessionResumeOutcome.UnknownOutcome, result.Outcome);
        Assert.False(result.IsReplacementEligible);
        Assert.DoesNotContain("no rollout", result.Failure!.RedactedDiagnostic ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmbiguousProviderErrorPlusExactAbsenceIsVerifiedUnavailable()
    {
        string home = Directory.CreateTempSubdirectory("codex-resume-absent-").FullName;
        var process = new ScriptedAppServerProcess
        {
            RejectResume = true,
            ResumeErrorCode = -32600,
            CodexHome = home,
        };
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());

        SessionResumeResult result = await runtime.ResumeSessionAsync(new SessionResumeRequest(
            ResumeSpec(), new ProviderSessionReference("codex", "thread-old"), Profile()));

        Assert.Equal(SessionResumeOutcome.UnavailableSession, result.Outcome);
        Assert.True(result.IsReplacementEligible);
        Assert.Equal("UnavailableSession", result.Failure!.Classification);
        Assert.False(result.Transport.TurnSubmitted);
    }

    [Theory]
    [InlineData(SessionOperationSupport.Unknown)]
    [InlineData(SessionOperationSupport.Unsupported)]
    public async Task NonSupportedResumeIsGatedBeforeAnyProviderFrame(SessionOperationSupport support)
    {
        var process = new ScriptedAppServerProcess();
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());

        SessionResumeResult result = await runtime.ResumeSessionAsync(new SessionResumeRequest(
            ResumeSpec(), new ProviderSessionReference("codex", "thread-old"), Profile(resume: support)));

        Assert.Equal(SessionResumeOutcome.DeterministicProtocolFailure, result.Outcome);
        Assert.Empty(process.Methods);
        Assert.False(result.Transport.RequestSubmitted);
    }

    [Fact]
    public async Task EagerFreshCreateProducesAnIdWithZeroTurns()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);
        AgentSessionSpec spec = ResumeSpec();
        spec = new AgentSessionSpec(
            spec.SessionId, spec.RepositoryId, spec.Role, spec.Sandbox, spec.Effort,
            spec.WorkingDirectory, spec.StartupOptions, resumeThreadId: null,
            operationPermissionProfile: spec.OperationPermissionProfile);

        SessionCreateResult result = await runtime.CreateSessionAsync(new SessionCreateRequest(
            spec, Profile(), "create-1"));

        Assert.True(result.Succeeded);
        Assert.Equal("thread-xyz", result.Created!.ThreadId);
        Assert.Equal(0, result.Session!.CompletedTurns);
        Assert.Equal(["initialize", "initialized", "thread/start"], process.Methods);
        await runtime.CloseSessionAsync(result.Session);
    }

    [Fact]
    public async Task LegacyResumeBypassIsRejectedBeforeProcessLaunch()
    {
        var process = new ScriptedAppServerProcess();
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());

        await Assert.ThrowsAsync<SessionOperationProfileGateException>(() => runtime.OpenSessionAsync(ResumeSpec()));

        Assert.Empty(process.Methods);
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

    [Fact]
    public async Task CertifiedThreadReadUsesInitializeAndReadWithoutStartingOrResumingAThread()
    {
        var process = new ScriptedAppServerProcess();
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());
        AgentSessionSpec spec = ResumeSpec();

        SessionContentResult result = await runtime.ReadSessionAsync(new SessionContentRequest(
            spec,
            new ProviderSessionReference("codex", "thread-old"),
            Profile(read: SessionOperationSupport.Supported)));

        Assert.True(result.Succeeded);
        Assert.Equal(["initialize", "initialized", "thread/read"], process.Methods);
        Assert.Equal(["previous request", "previous answer"], result.Records!.Select(record => record.Text));
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task UnknownThreadReadSupportEmitsNoProviderFrame()
    {
        var process = new ScriptedAppServerProcess();
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());

        SessionContentResult result = await runtime.ReadSessionAsync(new SessionContentRequest(
            ResumeSpec(),
            new ProviderSessionReference("codex", "thread-old"),
            Profile(read: SessionOperationSupport.Unknown)));

        Assert.False(result.Succeeded);
        Assert.Empty(process.Methods);
        Assert.Equal("OperationProfileGate", result.Failure!.Classification);
    }

    [Fact]
    public async Task CertifiedForkReturnsDistinctLiveChildWithoutImplicitContextTurn()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);
        AgentSessionSpec baseSpec = ResumeSpec();
        var forkSpec = new AgentSessionSpec(
            SessionIdentity.New(), baseSpec.RepositoryId, baseSpec.Role, baseSpec.Sandbox, baseSpec.Effort,
            baseSpec.WorkingDirectory);

        SessionForkResult result = await runtime.ForkSessionAsync(new SessionForkRequest(
            forkSpec,
            new ProviderSessionReference("codex", "thread-old"),
            Profile(fork: SessionOperationSupport.Supported),
            "fork-1"));

        Assert.True(result.Succeeded);
        Assert.Equal("thread-fork-child", result.Child!.ThreadId);
        Assert.Equal("thread-old", result.Parent.ThreadId);
        Assert.Equal("history-digest", result.HistoryDigest);
        Assert.Equal(["initialize", "initialized", "thread/fork"], process.Methods);
        Assert.Equal(0, result.Session!.CompletedTurns);
        await runtime.CloseSessionAsync(result.Session);
    }

    [Fact]
    public async Task UnknownForkSupportEmitsNoProviderFrame()
    {
        var process = new ScriptedAppServerProcess();
        AgentRuntime runtime = NewRuntime(process, new AgentSessionRegistry());
        AgentSessionSpec baseSpec = ResumeSpec();
        var forkSpec = new AgentSessionSpec(
            SessionIdentity.New(), baseSpec.RepositoryId, baseSpec.Role, baseSpec.Sandbox, baseSpec.Effort,
            baseSpec.WorkingDirectory);

        SessionForkResult result = await runtime.ForkSessionAsync(new SessionForkRequest(
            forkSpec,
            new ProviderSessionReference("codex", "thread-old"),
            Profile(fork: SessionOperationSupport.Unknown),
            "fork-1"));

        Assert.False(result.Succeeded);
        Assert.Empty(process.Methods);
    }
}

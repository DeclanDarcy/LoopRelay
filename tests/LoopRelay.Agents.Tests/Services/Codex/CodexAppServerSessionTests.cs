using System.Text.Json;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Agents.Tests.Services.Process;
using LoopRelay.Permissions.Abstractions.Evaluation;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Codex;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Permissions.Services.Parsing;
using LoopRelay.Permissions.Services.Security;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexAppServerSessionTests
{
    private static AgentSessionSpec Spec(bool requiresApproval = false) => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.OperationalExecution,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: requiresApproval),
        new EffortProfile(AgentEffortLevel.Medium),
        workingDirectory: "/repo");

    private static CodexAppServerSession NewSession(ScriptedAppServerProcess process) =>
        new(Spec(), process, new DeterministicAgentTokenEstimator(), PermissionGateway());

    [Fact]
    public async Task SingleTurnRunsHandshakeAndReturnsReplyWithReportedUsage()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal("reply 1", result.Output);
        Assert.Null(result.Diagnostics); // diagnostics are failure-only; a completed turn never carries them
        Assert.Equal(11, result.Usage.PromptTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
        Assert.Equal("u1", result.ProviderTurnId);
        Assert.Equal(AgentTurnTransportState.Terminal, result.TransportState);
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
    public async Task FailedTurnSurfacesFailedStateWithTheFailureMessageAsDiagnostics()
    {
        // The turn/completed frame carried { status: "failed", error: { message: "boom" } } — that message must
        // ride out on AgentTurnResult.Diagnostics so consumers surface the actual codex error, not a bare state.
        var process = new ScriptedAppServerProcess { TurnStatus = "failed" };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.NotNull(result.Diagnostics);
        Assert.Contains("boom", result.Diagnostics);
    }

    [Fact]
    public async Task FailedTurnWithoutAProtocolFailureMessageFallsBackToTheProcessErrorSnapshot()
    {
        var process = new ScriptedAppServerProcess
        {
            TurnStatus = "failed",
            TurnErrorMessage = null,
            ErrorSnapshot = "stderr tail: sandbox denied",
        };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal("stderr tail: sandbox denied", result.Diagnostics);
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
    public async Task SafeApprovalRequestsAreAcceptedAndDoNotBlockTheTurn()
    {
        var process = new ScriptedAppServerProcess { EmitApprovalRequest = true, ApprovalCommand = "dotnet build" };
        await using var session = new CodexAppServerSession(
            Spec(requiresApproval: true),
            process,
            new DeterministicAgentTokenEstimator(),
            PermissionGateway());

        AgentTurnResult result = await session.RunTurnAsync("hello");
        await process.ApprovalAccepted.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Contains(process.Writes, write => write.Contains("\"accept\""));
        Assert.False(ParamsOf(process, "thread/start").TryGetProperty("approvalPolicy", out _));
    }

    [Fact]
    public async Task DangerousApprovalRequestsAreDeclinedAndDoNotBlockTheTurn()
    {
        var process = new ScriptedAppServerProcess { EmitApprovalRequest = true, ApprovalCommand = "git push" };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult result = await session.RunTurnAsync("hello");
        await process.ApprovalDeclined.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Contains(process.Writes, write => write.Contains("\"decline\""));
    }

    [Fact]
    public async Task ScopedFileChangeApprovalMatchingOperationProfileIsAccepted()
    {
        var process = new ScriptedAppServerProcess
        {
            EmitApprovalRequest = true,
            EmitFileChangeApproval = true,
            ApprovalTargetPath = ".agents/details.md",
        };
        await using var session = new CodexAppServerSession(
            OperationSpec(".agents/details.md"),
            process,
            new DeterministicAgentTokenEstimator(),
            PermissionGateway());

        AgentTurnResult result = await session.RunTurnAsync("hello");
        await process.ApprovalAccepted.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Contains(process.Writes, write => write.Contains("\"accept\""));
    }

    [Fact]
    public async Task ScopedFileChangeApprovalOutsideOperationProfileIsDeclined()
    {
        var process = new ScriptedAppServerProcess
        {
            EmitApprovalRequest = true,
            EmitFileChangeApproval = true,
            ApprovalTargetPath = ".agents/plan.md",
        };
        await using var session = new CodexAppServerSession(
            OperationSpec(".agents/details.md"),
            process,
            new DeterministicAgentTokenEstimator(),
            PermissionGateway());

        AgentTurnResult result = await session.RunTurnAsync("hello");
        await process.ApprovalDeclined.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Contains(process.Writes, write => write.Contains("\"decline\""));
    }

    [Fact]
    public async Task PermissionEvaluationFailureDeclinesTheRequestInsteadOfHanging()
    {
        var process = new ScriptedAppServerProcess { EmitApprovalRequest = true, ApprovalCommand = "dotnet build" };
        await using var session = new CodexAppServerSession(
            Spec(),
            process,
            new DeterministicAgentTokenEstimator(),
            new ThrowingPermissionGateway());

        AgentTurnResult result = await session.RunTurnAsync("hello");
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

    // ---------------------------------------------------------------------------------------------------------
    // m10 (A) — app-server (persistent) WIRE-LEVEL certification. The existing session tests only capture method
    // NAMES; these parse the RAW captured outbound frames (process.Writes) to pin params.effort / params.sandbox /
    // params.approvalPolicy on the actual turn/start + thread/start frames, plus the no-MCP/no-tools guard.
    // ---------------------------------------------------------------------------------------------------------

    // The Decision-session spec the orchestrator's BuildDecisionSpec produces: read-only / approvals never /
    // EffortProfile(High, "xhigh"). The Decision session is the HELD-OPEN app-server path, so its wire frames must
    // carry exactly this posture.
    private static AgentSessionSpec DecisionSpec() => new(
        SessionIdentity.New(),
        "repo-decision",
        SessionRole.Decision,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
        workingDirectory: "/repo");

    private static JsonElement ParamsOf(ScriptedAppServerProcess process, string method)
    {
        string frame = process.Writes.First(line =>
        {
            using JsonDocument document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("method", out JsonElement m) && m.GetString() == method;
        });
        return JsonDocument.Parse(frame).RootElement.GetProperty("params").Clone();
    }

    [Fact]
    public async Task DecisionSessionTurnStartFrameCarriesXhighEffortOnTheWire()
    {
        // The held-open Decision session built from EffortProfile(High, "xhigh") must put params.effort == "xhigh"
        // on the actual turn/start frame — the identifier escape-hatch, NOT the "high" level mapping.
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(DecisionSpec(), process, new DeterministicAgentTokenEstimator());

        await session.RunTurnAsync("propose decisions");

        Assert.Equal("xhigh", ParamsOf(process, "turn/start").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task DecisionSessionThreadStartFrameCarriesReadOnlySandboxAndNeverApprovalOnTheWire()
    {
        // The Decision session's zero-permission posture must appear on the actual thread/start frame:
        // sandbox == "read-only" AND approvalPolicy == "never" (captured from the frame, not a method name).
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(DecisionSpec(), process, new DeterministicAgentTokenEstimator());

        await session.RunTurnAsync("propose decisions");

        JsonElement threadStart = ParamsOf(process, "thread/start");
        Assert.Equal("read-only", threadStart.GetProperty("sandbox").GetString());
        Assert.Equal("never", threadStart.GetProperty("approvalPolicy").GetString());
        Assert.Equal("/repo", threadStart.GetProperty("cwd").GetString());
    }

    // The CLI execution session's spec carries SandboxProfile("danger-full-access"), and the held-open
    // app-server path is the one that actually reaches codex (thread/start sets the sandbox per thread). This
    // pins that the full-access posture appears verbatim on the wire — the effective lever, not just a default.
    private static AgentSessionSpec ExecutionSpec() => new(
        SessionIdentity.New(),
        "repo-execution",
        SessionRole.OperationalExecution,
        new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.Medium),
        workingDirectory: "/repo");

    [Fact]
    public async Task ExecutionSessionThreadStartFrameCarriesDangerFullAccessSandboxOnTheWire()
    {
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(ExecutionSpec(), process, new DeterministicAgentTokenEstimator());

        await session.RunTurnAsync("continue executing the milestone");

        JsonElement threadStart = ParamsOf(process, "thread/start");
        Assert.Equal("danger-full-access", threadStart.GetProperty("sandbox").GetString());
        Assert.Equal("never", threadStart.GetProperty("approvalPolicy").GetString());
    }

    // m10 (A) LIVE-ONLY: real codex-cli 0.139 acceptance of params.effort=="xhigh" and sandbox=="read-only" on a
    // real app-server thread CANNOT run in-session (it needs codex login + a live process), so it is kept OFF the
    // default CI path. The wire-level frame shape it would exercise is fully pinned by the captured-frame tests
    // above; this skip-fact documents the manual certification step a release engineer runs against a live login.
    [Fact(Skip = "requires codex login; manual/live cert — verify codex-cli 0.139 accepts effort=xhigh + sandbox=read-only on a live app-server thread")]
    public void LiveCertification_RealCodexAcceptsXhighEffortAndReadOnlySandbox()
    {
        // Manual steps: (1) `codex login`; (2) open a held-open Decision app-server thread via the production
        // launcher with BuildDecisionSpec's posture; (3) submit a turn and confirm codex accepts effort=xhigh and
        // the read-only/never sandbox without error. No in-session assertion — see the captured-frame tests above.
    }

    [Fact(Skip = "requires codex login; manual/live cert — verify read-only app-server sessions request approval before file edits")]
    public void LiveCertification_ReadOnlyAppServerRequestsApprovalBeforeFileEdits()
    {
        // Manual steps: open a scoped artifact operation session with sandbox=read-only and approval on-request,
        // ask codex to edit an allowed file, and confirm the app-server emits item/fileChange/requestApproval
        // before any repository mutation is applied.
    }

    [Fact(Skip = "requires codex login; manual/live cert — verify file-change approvals expose exact target paths")]
    public void LiveCertification_FileChangeApprovalExposesExactTargetPath()
    {
        // Manual steps: capture the raw item/fileChange/requestApproval payload for a scoped artifact edit and
        // confirm it contains a target path precise enough to compare against the operation profile. If codex only
        // exposes a broad grantRoot, scoped operations must decline it.
    }

    [Fact(Skip = "requires codex login; manual/live cert — verify declined scoped approvals do not hang the turn")]
    public void LiveCertification_DeclinedScopedApprovalDoesNotHang()
    {
        // Manual steps: force a scoped operation to request a disallowed edit, verify LoopRelay replies decline,
        // and confirm codex fails or continues without leaving the turn parked indefinitely.
    }

    [Fact(Skip = "requires codex login; manual/live cert — verify accepted scoped approvals apply only requested writes")]
    public void LiveCertification_AcceptedScopedApprovalAllowsOnlyRequestedWrite()
    {
        // Manual steps: approve an exact allowed scoped edit, confirm that write succeeds, and confirm a subsequent
        // non-profile write still triggers and receives a declined approval.
    }

    [Fact]
    public async Task HeldOpenFramesCarryNoMcpServersOrToolsProperties()
    {
        // No-MCP/no-tools regression guard: the held-open thread/start + turn/start frames must NOT carry any
        // 'mcpServers'/'mcp'/'tools' surface — the app-server posture is sandbox/approval/effort only.
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(DecisionSpec(), process, new DeterministicAgentTokenEstimator());

        await session.RunTurnAsync("propose decisions");

        foreach (string method in new[] { "thread/start", "turn/start" })
        {
            JsonElement p = ParamsOf(process, method);
            Assert.False(p.TryGetProperty("mcpServers", out _), $"{method} must not carry mcpServers");
            Assert.False(p.TryGetProperty("mcp", out _), $"{method} must not carry mcp");
            Assert.False(p.TryGetProperty("tools", out _), $"{method} must not carry tools");
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // m10 (B) — HELD-OPEN CodexAppServerSession behavior under stress: long output, idle-then-second-turn,
    // cancel-mid-turn, and process-death-mid-turn. The process is the live held-open seam; these certify it never
    // deadlocks, reuses the thread, releases the turn gate on a mere turn cancel WITHOUT killing the process, and
    // fails fast (pumpEnded guard) after the process dies.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task LongOutputIsSurfacedInOrderWithoutDeadlock()
    {
        // Thousands of deltas must all surface, in order, with no deadlock between the pump, the writer, and the
        // caller's onChunk drain.
        const int deltas = 5000;
        var process = new ScriptedAppServerProcess { DeltaCount = deltas };
        await using CodexAppServerSession session = NewSession(process);

        var received = new List<string>();
        AgentTurnResult result = await session.RunTurnAsync("hello", chunk =>
        {
            received.Add(chunk.Content);
            return Task.CompletedTask;
        }).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal(deltas, received.Count);
        // In order: d0|, d1|, ... and the concatenated output preserves that order.
        Assert.Equal("d0|", received[0]);
        Assert.Equal($"d{deltas - 1}|", received[^1]);
        Assert.StartsWith("d0|d1|d2|", result.Output);
        Assert.EndsWith($"d{deltas - 1}|", result.Output);
    }

    [Fact]
    public async Task IdleThenSecondTurnReusesTheThreadAndDoesNotDisposeBetweenTurns()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        await session.RunTurnAsync("one");
        Assert.Equal(AgentProcessState.Running, session.State); // NOT disposed while idle between turns
        AgentTurnResult second = await session.RunTurnAsync("two");

        Assert.Equal("reply 2", second.Output);
        Assert.Equal(2, session.CompletedTurns);
        Assert.Equal(1, process.Methods.Count(m => m == "thread/start")); // the thread was reused, not recreated
        Assert.Equal(2, process.Methods.Count(m => m == "turn/start"));
    }

    [Fact]
    public async Task CancelMidTurnThrowsReleasesTheGateForASubsequentTurnAndDoesNotKillTheProcess()
    {
        // A mere TURN cancellation (the caller's token) must: throw OperationCanceledException, release the turn
        // gate so a SUBSEQUENT turn still runs, and leave the process ALIVE — only DisposeAsync/CancelAsync kill it.
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new ScriptedAppServerProcess { HoldBeforeCompletion = hold.Task };
        await using CodexAppServerSession session = NewSession(process);

        using var cts = new CancellationTokenSource();
        Task<AgentTurnResult> turn = session.RunTurnAsync("first", cancellationToken: cts.Token);
        await process.TurnInFlight; // the turn emitted its deltas and is parked, holding the gate
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => turn);
        Assert.Equal(AgentProcessState.Running, session.State); // the process was NOT killed by the turn cancel

        // The gate was released, so a fresh (uncancelled) turn on the SAME process still runs to completion.
        process.HoldBeforeCompletion = null; // the next turn completes normally
        AgentTurnResult next = await session.RunTurnAsync("second").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AgentTurnState.Completed, next.State);
    }

    [Fact]
    public async Task ProcessDeathMidTurnFailsTheInFlightTurnAndASubsequentTurnFailsFastViaThePumpEndedGuard()
    {
        // The process dies under an in-flight turn (its output channel completes mid-turn). The in-flight
        // RunTurnAsync must return Failed / throw and release pending waiters, and a SUBSEQUENT RunTurnAsync must
        // FAIL FAST via the pumpEnded guard rather than hang forever.
        var process = new ScriptedAppServerProcess { KillAfterDeltas = true };
        await using CodexAppServerSession session = NewSession(process);

        AgentTurnResult inflight = await RunToCompletionOrFault(session, "first");
        Assert.NotEqual(AgentTurnState.Completed, inflight.State);

        // A second turn must not hang: the pumpEnded guard surfaces an IOException quickly.
        await Assert.ThrowsAnyAsync<Exception>(
            () => session.RunTurnAsync("second").WaitAsync(TimeSpan.FromSeconds(5)));
    }

    // Runs a turn that may end via the pump completing the turn (process death) rather than a normal terminal —
    // returning the result if the session yields one, or a synthesized Failed result if it threw.
    private static async Task<AgentTurnResult> RunToCompletionOrFault(CodexAppServerSession session, string prompt)
    {
        try
        {
            return await session.RunTurnAsync(prompt).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            return new AgentTurnResult(0, AgentTurnState.Failed, string.Empty, AgentTokenUsage.Zero);
        }
    }

    private static IPermissionGateway PermissionGateway() =>
        new PermissionGateway(
            new CodexPermissionAdapter(),
            new PermissionHandler(
                new CommandParser(),
                new CommandCanonicalizer(),
                new Sha256FingerprintService(),
                new InMemoryPermissionCache(),
                new PermissionEvaluatorEngine(),
                new InvariantGuard()),
            new OperationPermissionHandler());

    private static AgentSessionSpec OperationSpec(string allowedWrite) => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.OperationalExecution,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: true),
        new EffortProfile(AgentEffortLevel.Medium),
        workingDirectory: "/repo",
        operationPermissionProfile: new OperationPermissionProfile(
            "test-operation",
            "/repo",
            [".agents/plan.md"],
            [],
            [allowedWrite],
            []));

    private sealed class ThrowingPermissionGateway : IPermissionGateway
    {
        public byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory) =>
            throw new InvalidOperationException("permission engine failed");
    }

    private static AgentSessionSpec ResumeSpec(string threadId) => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.Decision,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
        workingDirectory: "/repo",
        resumeThreadId: threadId);

    private static SessionContinuityProfile ResumeProfile() => new(
        "codex", "test", "0.142.5", "codex", "app-server-v2", "schema",
        new Dictionary<string, bool> { ["experimentalApi"] = true },
        new Dictionary<string, string>(),
        new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
        {
            [SessionContinuityOperation.Resume] = new SessionOperationSupportDescriptor(
                SessionOperationSupport.Supported,
                "v2",
                new Dictionary<string, SessionParameterSupport>
                {
                    [SessionContinuityProfile.ExcludeTurnsParameter] =
                        new(SessionOperationSupport.Supported, "test"),
                },
                "load", "same-id", "none", "exact-id", "test"),
        },
        256_000, "test", "test", negotiatedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task ResumeSpecSendsThreadResumeInsteadOfThreadStartAndAddressesTurnsAtTheResumedThread()
    {
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(
            ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator(), null, ResumeProfile());

        await session.EnsureReadyAsync();
        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal(AgentTurnState.Completed, result.State);
        Assert.Equal(["initialize", "initialized", "thread/resume", "turn/start"], process.Methods);
        Assert.Equal("thread-old", session.ThreadId);
        Assert.Equal("thread-old", ParamsOf(process, "turn/start").GetProperty("threadId").GetString());
    }

    [Fact]
    public async Task ResumeFrameCarriesTheSessionPostureAndExcludeTurns()
    {
        var process = new ScriptedAppServerProcess();
        await using var session = new CodexAppServerSession(
            ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator(), null, ResumeProfile());

        await session.EnsureReadyAsync();

        JsonElement p = ParamsOf(process, "thread/resume");
        Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
        Assert.Equal("/repo", p.GetProperty("cwd").GetString());
        Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
        Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
        Assert.True(p.GetProperty("excludeTurns").GetBoolean());
    }

    [Fact]
    public async Task RejectedResumePreservesTheStructuredResponseFromEnsureReady()
    {
        var process = new ScriptedAppServerProcess { RejectResume = true };
        await using var session = new CodexAppServerSession(
            ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator(), null, ResumeProfile());

        CodexAppServerRequestException ex =
            await Assert.ThrowsAsync<CodexAppServerRequestException>(() => session.EnsureReadyAsync());
        Assert.Equal("thread/resume", ex.ProviderMethod);
        Assert.Equal(-32602, ex.Response.ErrorCode);
        Assert.Equal("excludeTurns", ex.Response.ErrorData.GetProperty("field").GetString());
    }

    [Fact]
    public async Task ThreadIdIsNullBeforeTheHandshakeAndSetAfterIt()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        Assert.Null(session.ThreadId);
        await session.RunTurnAsync("hello");
        Assert.Equal("thread-xyz", session.ThreadId);
    }

    [Fact]
    public async Task NonResumeEnsureReadyRunsTheNormalHandshakeExactlyOnce()
    {
        var process = new ScriptedAppServerProcess();
        await using CodexAppServerSession session = NewSession(process);

        await session.EnsureReadyAsync();
        await session.RunTurnAsync("hello");

        Assert.Equal(["initialize", "initialized", "thread/start", "turn/start"], process.Methods);
    }
}

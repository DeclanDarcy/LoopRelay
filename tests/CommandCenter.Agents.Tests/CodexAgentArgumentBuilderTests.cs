using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Agents.Tests;

public sealed class CodexAgentArgumentBuilderTests
{
    private static AgentSessionSpec Spec(
        bool canWrite,
        bool requiresApproval,
        AgentEffortLevel effort,
        string? workingDirectory = "/repo") =>
        new(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.OperationalExecution,
            // Identifier is the codex sandbox mode the persistent path now emits verbatim, so it must be a
            // real mode string that matches the write posture (not a placeholder).
            new SandboxProfile(canWrite ? "workspace-write" : "read-only", canWrite, CanAccessNetwork: false, requiresApproval),
            new EffortProfile(effort),
            workingDirectory);

    [Fact]
    public void OneShotBuildsExecJsonArgsWithStdinAndWorkingDirectory()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Medium),
            AgentSessionMode.OneShot);

        Assert.Equal("exec", args[0]);
        Assert.Contains("--json", args);
        Assert.Contains("--cd", args);
        Assert.Contains("/repo", args);
        Assert.Equal("-", args[^1]); // stdin prompt is the trailing positional
        Assert.DoesNotContain("--sandbox", args); // exec path intentionally omits the sandbox flag
    }

    // One-shot turns may run with --cd pointed at a temp sandbox workspace outside any trusted git
    // repository; without --skip-git-repo-check codex exits 1 immediately ("Not inside a trusted
    // directory and --skip-git-repo-check was not specified") and the turn never runs. The flag must
    // precede the trailing positional "-" (the stdin prompt) or codex parses it as prompt input.
    [Fact]
    public void OneShotSkipsGitRepoTrustCheck_BeforeTheTrailingStdinPositional()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Medium),
            AgentSessionMode.OneShot);

        Assert.Contains("--skip-git-repo-check", args);
        Assert.Equal("-", args[^1]); // the positional stdin marker stays last
    }

    // The persistent/app-server path talks JSON-RPC from the repo itself — the trust check applies
    // there and is intentionally NOT skipped.
    [Fact]
    public void PersistentDoesNotSkipGitRepoTrustCheck()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Medium),
            AgentSessionMode.Persistent);

        Assert.DoesNotContain("--skip-git-repo-check", args);
    }

    [Fact]
    public void PersistentBuildsAppServerStdioArgs()
    {
        // codex 0.139 removed `codex proto`; the held-open path is the app-server JSON-RPC transport.
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.High),
            AgentSessionMode.Persistent);

        Assert.Contains("app-server", args);
        Assert.Contains("--listen", args);
        Assert.Contains("stdio://", args);
        Assert.Contains("--ask-for-approval", args);
        Assert.Contains("never", args);
        Assert.DoesNotContain("proto", args);
        Assert.DoesNotContain("exec", args);
    }

    // The exec/one-shot path intentionally omits the --sandbox flag (the sandbox posture is left to codex's
    // own configuration), so no sandbox value is emitted regardless of the spec's write posture.
    [Fact]
    public void OneShotDoesNotPassSandboxFlag()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: false, requiresApproval: true, AgentEffortLevel.Low),
            AgentSessionMode.OneShot);

        Assert.DoesNotContain("--sandbox", args);
        Assert.DoesNotContain("read-only", args);
        Assert.DoesNotContain("workspace-write", args);
    }

    // The persistent/app-server path still maps the sandbox posture onto an explicit --sandbox flag.
    [Fact]
    public void PersistentSandboxMapsPosture()
    {
        IReadOnlyList<string> readOnly = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: false, requiresApproval: false, AgentEffortLevel.Low),
            AgentSessionMode.Persistent);
        Assert.Contains("--sandbox", readOnly);
        Assert.Contains("read-only", readOnly);

        IReadOnlyList<string> writable = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Low),
            AgentSessionMode.Persistent);
        Assert.Contains("workspace-write", writable);
    }

    // The persistent path now emits the sandbox Identifier verbatim, so codex's full-access mode
    // ("danger-full-access") — used by the CLI execution session — reaches the --sandbox flag. The prior
    // bool-only mapping could only ever produce read-only/workspace-write, making full access unreachable.
    [Fact]
    public void PersistentSandboxEmitsDangerFullAccessIdentifierVerbatim()
    {
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.OperationalExecution,
            new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.Medium),
            workingDirectory: "/repo");

        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(spec, AgentSessionMode.Persistent);

        Assert.Contains("--sandbox", args);
        Assert.Contains("danger-full-access", args);
    }

    [Fact]
    public void ApprovalsOffEmitsNeverPolicy()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.High),
            AgentSessionMode.OneShot);

        Assert.Contains("approval_policy=\"never\"", args);
    }

    // The exec/one-shot path runs fully unattended: approval_policy="never" is emitted unconditionally
    // (the prior RequiresApproval gate was removed), so a CLI turn never blocks waiting for approval.
    [Fact]
    public void OneShotAlwaysEmitsNeverApprovalPolicy()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: false, requiresApproval: true, AgentEffortLevel.Low),
            AgentSessionMode.OneShot);

        Assert.Contains("approval_policy=\"never\"", args);
    }

    [Fact]
    public void EffortLevelMapsToReasoningConfig()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.High),
            AgentSessionMode.OneShot);

        Assert.Contains("model_reasoning_effort=\"high\"", args);
    }

    [Fact]
    public void EffortIdentifierOverridesLevelMapping()
    {
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.Planning,
            new SandboxProfile("sandbox", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            workingDirectory: "/repo");

        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(spec, AgentSessionMode.OneShot);

        Assert.Contains("model_reasoning_effort=\"xhigh\"", args);
    }

    // m10 (A) Medium-value certification: AgentEffortLevel.Medium with no identifier maps to "medium" — the tier
    // StartExecution/ContinueExecution run at. (The exec-path "xhigh" and isolated-protocol "high" are already
    // pinned; this completes the level map's middle rung.)
    [Fact]
    public void MediumEffortMapsToMediumReasoningConfig()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Medium),
            AgentSessionMode.OneShot);

        Assert.Contains("model_reasoning_effort=\"medium\"", args);
    }

    // m10 (A) no-MCP/no-tools regression guard for the EXEC path: the only -c config keys present are
    // approval_policy + model_reasoning_effort + whatever explicit StartupOptions the caller passes. No
    // tools/web_search/include_plan_tool surface is ever injected by the builder.
    [Fact]
    public void ExecArgsCarryNoToolsWebSearchOrPlanToolConfigKeys()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.Medium),
            AgentSessionMode.OneShot);

        // Every -c key is one of the two governed defaults — nothing else.
        var configKeys = new List<string>();
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == "-c")
            {
                configKeys.Add(args[i + 1].Split('=')[0]);
            }
        }

        Assert.Equal(new[] { "approval_policy", "model_reasoning_effort" }, configKeys);
        Assert.DoesNotContain(args, a => a.Contains("tools", StringComparison.Ordinal));
        Assert.DoesNotContain(args, a => a.Contains("web_search", StringComparison.Ordinal));
        Assert.DoesNotContain(args, a => a.Contains("include_plan_tool", StringComparison.Ordinal));
        Assert.DoesNotContain(args, a => a.Contains("mcp", StringComparison.Ordinal));
    }

    // m10 (A): explicit StartupOptions ARE emitted as additional -c keys (so the guard above is a real constraint,
    // not vacuous) — and ONLY those the caller passed.
    [Fact]
    public void ExplicitStartupOptionsAreEmittedAsAdditionalConfigKeys()
    {
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.OperationalExecution,
            new SandboxProfile("sandbox", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.Medium),
            workingDirectory: "/repo",
            startupOptions: new Dictionary<string, string> { ["model"] = "\"gpt-5\"" });

        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(spec, AgentSessionMode.OneShot);

        Assert.Contains("model=\"gpt-5\"", args);
    }
}

using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

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
            new SandboxProfile("sandbox", canWrite, CanAccessNetwork: false, requiresApproval),
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
        Assert.Contains("workspace-write", args);
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

    [Fact]
    public void ReadOnlySandboxMapsToReadOnly()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: false, requiresApproval: true, AgentEffortLevel.Low),
            AgentSessionMode.OneShot);

        Assert.Contains("read-only", args);
        Assert.DoesNotContain("workspace-write", args);
    }

    [Fact]
    public void ApprovalsOffEmitsNeverPolicy()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: true, requiresApproval: false, AgentEffortLevel.High),
            AgentSessionMode.OneShot);

        Assert.Contains("approval_policy=\"never\"", args);
    }

    [Fact]
    public void ApprovalsRequiredOmitsNeverPolicy()
    {
        IReadOnlyList<string> args = CodexAgentArgumentBuilder.Build(
            Spec(canWrite: false, requiresApproval: true, AgentEffortLevel.Low),
            AgentSessionMode.OneShot);

        Assert.DoesNotContain("approval_policy=\"never\"", args);
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
}

using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Cli.Services;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Permissions.Models;
using Xunit;

namespace LoopRelay.Cli.Tests.Services;

public class AgentSpecsTests
{
    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    [Fact]
    public void Operational_IsWorkspaceWriteNoApprovalAtGivenEffort()
    {
        AgentSessionSpec spec = AgentSpecs.Operational(Repo, AgentEffortLevel.Medium, identifier: null);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("workspace-write", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.Medium, spec.Effort.Level);
        Assert.Null(spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    // The CLI execution session opts into codex's full-access sandbox (matching the legacy CodexExecutionProvider's
    // deliberate danger-full-access policy). The default posture is unchanged — only the explicit override differs,
    // so the context-update evolution one-shot (the other Operational caller) keeps workspace-write.
    [Fact]
    public void Operational_WithDangerFullAccessSandbox_GrantsFullAccessAndNetwork()
    {
        AgentSessionSpec spec = AgentSpecs.Operational(
            Repo, AgentEffortLevel.Medium, identifier: null, sandboxIdentifier: "danger-full-access");

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
    }

    [Fact]
    public void Decision_IsReadOnlyHighXhigh()
    {
        AgentSessionSpec spec = AgentSpecs.Decision(Repo);

        Assert.Equal(SessionRole.Decision, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Decision_WithResumeThreadId_CarriesItOnTheSpec()
    {
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

        AgentSessionSpec spec = AgentSpecs.Decision(repo, "thread-old");

        Assert.Equal("thread-old", spec.ResumeThreadId);
        // The resume overload must not perturb the decision posture.
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal("/repo", spec.WorkingDirectory);
    }

    [Fact]
    public void Decision_Default_HasNoResumeThreadId()
    {
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

        Assert.Null(AgentSpecs.Decision(repo).ResumeThreadId);
    }

    [Fact]
    public void ScopedArtifactOperation_IsReadOnlyApprovalGatedWithOperationProfile()
    {
        var profile = new OperationPermissionProfile(
            "operational-context-evolution",
            Repo.Path,
            [".agents/operational_context.md"],
            [],
            [".agents/operational_context.md"],
            []);

        AgentSessionSpec spec = AgentSpecs.ScopedArtifactOperation(
            Repo,
            AgentEffortLevel.High,
            "xhigh",
            profile);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.True(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
        Assert.Same(profile, spec.OperationPermissionProfile);
    }
}

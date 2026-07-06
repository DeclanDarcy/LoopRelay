using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class AgentSpecsTests
{
    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    [Fact]
    public void PlanAuthoring_IsDangerFullAccessAtRepoRootWithXhighEffort()
    {
        AgentSessionSpec spec = Cli.AgentSpecs.PlanAuthoring(Repo);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal(Repo.Id.ToString("N"), spec.RepositoryId);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Review_IsReadOnlyZeroPermissionAtRepoRootWithXhighEffort()
    {
        AgentSessionSpec spec = Cli.AgentSpecs.Review(Repo);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void SandboxedOneShot_IsWorkspaceWriteAtGivenDirectoryWithXhighEffort()
    {
        const string sandboxRoot = "/tmp/plan-cli-sandbox";

        AgentSessionSpec spec = Cli.AgentSpecs.SandboxedOneShot(Repo, sandboxRoot);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("workspace-write", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(sandboxRoot, spec.WorkingDirectory);
    }

    [Fact]
    public void AllFactories_MintFreshSessionIdentityPerCall()
    {
        AgentSessionSpec first = Cli.AgentSpecs.PlanAuthoring(Repo);
        AgentSessionSpec second = Cli.AgentSpecs.PlanAuthoring(Repo);

        Assert.NotEqual(first.SessionId, second.SessionId);
    }
}

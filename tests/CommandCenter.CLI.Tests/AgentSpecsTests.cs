using CommandCenter.Cli;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

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
}

using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Plan.Cli.Services.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Cli;

public class AgentSpecsTests
{
    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    [Fact]
    public void PlanAuthoring_IsDangerFullAccessAtRepoRootWithXhighEffort()
    {
        AgentSessionSpec spec = AgentSpecs.PlanAuthoring(Repo);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal(Repo.Id.ToString("N"), spec.RepositoryId);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffort.XHigh, spec.Effort);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Review_IsReadOnlyZeroPermissionAtRepoRootWithXhighEffort()
    {
        AgentSessionSpec spec = AgentSpecs.Review(Repo);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffort.XHigh, spec.Effort);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void ScopedArtifactOperation_IsDangerFullAccessWithoutApprovalAtRepoRootWithProfile()
    {
        var profile = new OperationPermissionProfile(
            "collect-details",
            Repo.Path,
            [".agents/plan.md"],
            [],
            [".agents/details.md"],
            []);

        AgentSessionSpec spec = AgentSpecs.ScopedArtifactOperation(Repo, profile);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffort.XHigh, spec.Effort);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
        Assert.Same(profile, spec.OperationPermissionProfile);
    }

    [Fact]
    public void AllFactories_MintFreshSessionIdentityPerCall()
    {
        AgentSessionSpec first = AgentSpecs.PlanAuthoring(Repo);
        AgentSessionSpec second = AgentSpecs.PlanAuthoring(Repo);

        Assert.NotEqual(first.SessionId, second.SessionId);
    }
}

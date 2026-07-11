using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Agents;

public class AgentSpecsTests
{
    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
    private static readonly BrainConfiguration Brain =
        new(AgentModel.Gpt56Luna, AgentEffort.Low);
    private static readonly ValidatedExecutionRecommendation Execution =
        ExecutionRecommendationContract.ValidatePair(
            "prompt",
            ExecutionRecommendationContract.SerializePersisted(
                ExecutionRecommendationContract.Bind(
                    "prompt",
                    new ExecutionRecommendation(AgentModel.Gpt56Terra, AgentEffort.Medium))));

    [Fact]
    public void BrainOperational_UsesInjectedBrainConfiguration()
    {
        AgentSessionSpec spec = AgentSpecs.BrainOperational(Repo, Brain);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentModel.Gpt56Luna, spec.Model);
        Assert.Equal(AgentEffort.Low, spec.Effort);
        Assert.Equal(AgentConfigurationAuthority.Brain, spec.ConfigurationAuthority);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Execution_UsesValidatedRecommendationAndFullAccess()
    {
        AgentSessionSpec spec = AgentSpecs.Execution(Repo, Execution);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentModel.Gpt56Terra, spec.Model);
        Assert.Equal(AgentEffort.Medium, spec.Effort);
        Assert.Equal(AgentConfigurationAuthority.Execution, spec.ConfigurationAuthority);
    }

    [Fact]
    public void Decision_IsReadOnlyAndUsesBrainConfiguration()
    {
        AgentSessionSpec spec = AgentSpecs.Decision(Repo, Brain);

        Assert.Equal(SessionRole.Decision, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentModel.Gpt56Luna, spec.Model);
        Assert.Equal(AgentEffort.Low, spec.Effort);
        Assert.Equal(AgentConfigurationAuthority.Brain, spec.ConfigurationAuthority);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Decision_WithResumeThreadId_CarriesItOnTheSpec()
    {
        AgentSessionSpec spec = AgentSpecs.Decision(Repo, Brain, "thread-old");

        Assert.Equal("thread-old", spec.ResumeThreadId);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.Equal(AgentEffort.Low, spec.Effort);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Decision_Default_HasNoResumeThreadId() =>
        Assert.Null(AgentSpecs.Decision(Repo, Brain).ResumeThreadId);

    [Fact]
    public void Review_IsPlanningReadOnlyAndUsesBrainConfiguration()
    {
        AgentSessionSpec spec = AgentSpecs.Review(Repo, Brain);

        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentModel.Gpt56Luna, spec.Model);
        Assert.Equal(AgentEffort.Low, spec.Effort);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void ScopedArtifactOperation_IsDangerFullAccessWithoutApprovalWithOperationProfile()
    {
        var profile = new OperationPermissionProfile(
            "operational-context-evolution",
            Repo.Path,
            [".agents/operational_context.md"],
            [],
            [".agents/operational_context.md"],
            []);

        AgentSessionSpec spec = AgentSpecs.ScopedArtifactOperation(Repo, Brain, profile);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentModel.Gpt56Luna, spec.Model);
        Assert.Equal(AgentEffort.Low, spec.Effort);
        Assert.Same(profile, spec.OperationPermissionProfile);
    }
}

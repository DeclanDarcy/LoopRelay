using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Primitives.Execution;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Cli;
using LoopRelay.Roadmap.Cli.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Execution;

public sealed class RoadmapExecutionBridgeTests
{
    [Fact]
    public void Default_execution_bridge_spec_is_workspace_write_no_network_with_approvals()
    {
        using var repo = new TempRepo();

        AgentSessionSpec spec = AgentSpecs.ExecutionBridge(repo.Repository);

        Assert.Equal("workspace-write", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.True(spec.Sandbox.RequiresApproval);
    }

    [Fact]
    public void Elevated_execution_bridge_spec_requires_reason_and_records_full_access_network()
    {
        using var repo = new TempRepo();

        AgentSessionSpec spec = AgentSpecs.ExecutionBridge(
            repo.Repository,
            RoadmapExecutionOptions.Elevated("Needs integration environment"));

        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.True(spec.Sandbox.RequiresApproval);
        Assert.Throws<RoadmapStepException>(() =>
            AgentSpecs.ExecutionBridge(repo.Repository, new RoadmapExecutionOptions("danger-full-access", AllowNetwork: true)));
    }

    [Fact]
    public async Task Approval_managed_execution_uses_held_open_session_and_records_default_trust_evidence()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));

        RoadmapExecutionTransportResult result = await new RoadmapExecutionBridge(
            runtime,
            repo.Artifacts,
            repo.Repository,
            new TestConsole()).RunAsync(CancellationToken.None);

        Assert.Equal(ExecutionTransportStatus.Completed, result.Status);
        Assert.Equal(1, runtime.OpenSessionCalls);
        Assert.Equal(1, runtime.PersistentTurnCalls);
        Assert.Equal(1, runtime.CloseSessionCalls);
        Assert.Equal(0, runtime.OneShotCalls);
        AgentSessionSpec spec = Assert.Single(runtime.OpenedSpecs);
        Assert.True(spec.Sandbox.RequiresApproval);
        Assert.NotNull(result.EvidencePath);
        string evidence = repo.Read(result.EvidencePath!);
        Assert.Contains("| Mode | Default |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Sandbox | workspace-write |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Network | Denied |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Approval | OnRequest |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Execution | PersistentSession |", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicitly_unattended_execution_preserves_one_shot_semantics()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));
        var options = new RoadmapExecutionOptions(RequiresApproval: false);

        await new RoadmapExecutionBridge(
            runtime,
            repo.Artifacts,
            repo.Repository,
            new TestConsole(),
            options).RunAsync(CancellationToken.None);

        Assert.Equal(0, runtime.OpenSessionCalls);
        Assert.Equal(1, runtime.OneShotCalls);
        Assert.False(Assert.Single(runtime.OneShotSpecs).Sandbox.RequiresApproval);
    }

    [Fact]
    public async Task Elevated_execution_evidence_records_reason()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));

        RoadmapExecutionTransportResult result = await new RoadmapExecutionBridge(
            runtime,
            repo.Artifacts,
            repo.Repository,
            new TestConsole(),
            RoadmapExecutionOptions.Elevated("Needs package registry access")).RunAsync(CancellationToken.None);

        Assert.NotNull(result.EvidencePath);
        string evidence = repo.Read(result.EvidencePath!);
        Assert.Contains("| Mode | Elevated |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Sandbox | danger-full-access |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Network | Allowed |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Elevated Reason | Needs package registry access |", evidence, StringComparison.Ordinal);
    }
}

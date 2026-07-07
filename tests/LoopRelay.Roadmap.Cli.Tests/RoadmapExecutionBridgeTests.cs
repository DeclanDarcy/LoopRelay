using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapExecutionBridgeTests
{
    [Fact]
    public void Default_execution_bridge_spec_is_workspace_write_no_network_with_approvals()
    {
        using var repo = new TempRepo();

        AgentSessionSpec spec = Cli.AgentSpecs.ExecutionBridge(repo.Repository);

        Assert.Equal("workspace-write", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.True(spec.Sandbox.RequiresApproval);
    }

    [Fact]
    public void Elevated_execution_bridge_spec_requires_reason_and_records_full_access_network()
    {
        using var repo = new TempRepo();

        AgentSessionSpec spec = Cli.AgentSpecs.ExecutionBridge(
            repo.Repository,
            Cli.RoadmapExecutionOptions.Elevated("Needs integration environment"));

        Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.True(spec.Sandbox.CanAccessNetwork);
        Assert.True(spec.Sandbox.RequiresApproval);
        Assert.Throws<Cli.RoadmapStepException>(() =>
            Cli.AgentSpecs.ExecutionBridge(repo.Repository, new Cli.RoadmapExecutionOptions("danger-full-access", AllowNetwork: true)));
    }

    [Fact]
    public async Task Approval_managed_execution_uses_held_open_session_and_records_default_trust_evidence()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));

        Cli.RoadmapExecutionTransportResult result = await new Cli.RoadmapExecutionBridge(
            runtime,
            repo.Artifacts,
            repo.Repository,
            new TestConsole()).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.ExecutionTransportStatus.Completed, result.Status);
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
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));
        var options = new Cli.RoadmapExecutionOptions(RequiresApproval: false);

        await new Cli.RoadmapExecutionBridge(
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
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "execute");
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("done"));

        Cli.RoadmapExecutionTransportResult result = await new Cli.RoadmapExecutionBridge(
            runtime,
            repo.Artifacts,
            repo.Repository,
            new TestConsole(),
            Cli.RoadmapExecutionOptions.Elevated("Needs package registry access")).RunAsync(CancellationToken.None);

        Assert.NotNull(result.EvidencePath);
        string evidence = repo.Read(result.EvidencePath!);
        Assert.Contains("| Mode | Elevated |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Sandbox | danger-full-access |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Network | Allowed |", evidence, StringComparison.Ordinal);
        Assert.Contains("| Elevated Reason | Needs package registry access |", evidence, StringComparison.Ordinal);
    }
}

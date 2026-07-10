using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Configuration;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal static class AgentSpecs
{
    // Retired CLI compatibility paths; active unified composition injects BrainConfiguration directly.
    public static AgentSessionSpec ReadOnlyPlanning(Repository repository) =>
        ReadOnlyPlanning(repository, CliSettingsLoader.Load().Brain);

    public static AgentSessionSpec ExecutionBridge(
        Repository repository,
        RoadmapExecutionOptions? options = null) =>
        ExecutionBridge(repository, CliSettingsLoader.Load().Brain, options);

    public static AgentSessionSpec ReadOnlyPlanning(Repository repository, BrainConfiguration brain) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path);

    public static AgentSessionSpec ExecutionBridge(
        Repository repository,
        BrainConfiguration brain,
        RoadmapExecutionOptions? options = null)
    {
        RoadmapExecutionOptions effectiveOptions = options ?? RoadmapExecutionOptions.Default;
        effectiveOptions.Validate();

        return new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile(
                effectiveOptions.SandboxIdentifier,
                CanWriteWorkspace: !string.Equals(effectiveOptions.SandboxIdentifier, "read-only", StringComparison.Ordinal),
                CanAccessNetwork: effectiveOptions.AllowNetwork,
                RequiresApproval: effectiveOptions.RequiresApproval),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path);
    }
}

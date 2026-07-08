using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.Execution;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal static class AgentSpecs
{
    public static AgentSessionSpec ReadOnlyPlanning(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    public static AgentSessionSpec ExecutionBridge(Repository repository, RoadmapExecutionOptions? options = null)
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
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);
    }
}

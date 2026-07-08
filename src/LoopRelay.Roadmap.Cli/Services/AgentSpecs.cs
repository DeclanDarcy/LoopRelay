using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

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

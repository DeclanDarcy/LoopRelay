using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Models;

namespace LoopRelay.Roadmap.Cli;

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

    public static AgentSessionSpec ExecutionBridge(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);
}

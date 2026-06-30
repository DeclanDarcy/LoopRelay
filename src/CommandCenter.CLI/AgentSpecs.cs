using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Cli;

/// <summary>
/// The two codex session postures copied verbatim from RepositoryOrchestrator.BuildOperationalSpec /
/// BuildDecisionSpec. RepositoryId namespaces the session registry; WorkingDirectory is the repo dir.
/// </summary>
internal static class AgentSpecs
{
    public static AgentSessionSpec Operational(Repository repository, AgentEffortLevel level, string? identifier) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(level, identifier),
            repository.Path);

    public static AgentSessionSpec Decision(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);
}

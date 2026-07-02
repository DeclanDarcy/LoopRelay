using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// The three codex session postures the planning pipeline needs, per the plan's Process Model table:
/// a danger-full-access authoring session (WritePlan/RevisePlan need to explore the codebase — sandboxed
/// codex cannot spawn child processes on Windows), a read-only zero-permission review session, and a
/// descriptive workspace-write posture for the isolated sandboxed one-shots (isolation there comes from
/// the seeded temp workspace's --cd, not from this sandbox identifier — see the plan's Process Model notes).
/// All three use SessionRole.Planning and EffortProfile(High, "xhigh").
/// </summary>
internal static class AgentSpecs
{
    public static AgentSessionSpec PlanAuthoring(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    public static AgentSessionSpec Review(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    public static AgentSessionSpec SandboxedOneShot(Repository repository, string workingDirectory) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            workingDirectory);
}

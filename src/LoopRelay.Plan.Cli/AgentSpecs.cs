using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Models;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Plan.Cli;

/// <summary>
/// The three codex session postures the planning pipeline needs, per the plan's Process Model table:
/// a danger-full-access authoring session (WritePlan/RevisePlan need to explore the codebase — sandboxed
/// codex cannot spawn child processes on Windows), a read-only zero-permission review session, and a
/// read-only approval-gated posture for scoped artifact operations.
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

    public static AgentSessionSpec ScopedArtifactOperation(
        Repository repository,
        OperationPermissionProfile operationProfile) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: true),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path,
            operationPermissionProfile: operationProfile);
}

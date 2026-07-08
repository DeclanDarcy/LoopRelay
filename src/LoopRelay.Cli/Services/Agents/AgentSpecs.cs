using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Cli.Services.Agents;

/// <summary>
/// The two codex session postures copied verbatim from RepositoryOrchestrator.BuildOperationalSpec /
/// BuildDecisionSpec. RepositoryId namespaces the session registry; WorkingDirectory is the repo dir.
/// </summary>
internal static class AgentSpecs
{
    // sandboxIdentifier is the codex sandbox mode. It defaults to "workspace-write" for ordinary operational
    // sessions; the execution session overrides it to "danger-full-access" so codex runs
    // unsandboxed — matching the legacy CodexExecutionProvider's deliberate policy. danger-full-access also
    // grants network, so CanAccessNetwork tracks it.
    public static AgentSessionSpec Operational(
        Repository repository,
        AgentEffortLevel level,
        string? identifier,
        string? workingDirectory = null,
        string sandboxIdentifier = "workspace-write") =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile(
                sandboxIdentifier,
                CanWriteWorkspace: true,
                CanAccessNetwork: sandboxIdentifier == "danger-full-access",
                RequiresApproval: false),
            new EffortProfile(level, identifier),
            workingDirectory ?? repository.Path);

    public static AgentSessionSpec Decision(Repository repository, string? resumeThreadId = null) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path,
            resumeThreadId: resumeThreadId);

    public static AgentSessionSpec ScopedArtifactOperation(
        Repository repository,
        AgentEffortLevel level,
        string? identifier,
        OperationPermissionProfile operationProfile) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: true),
            new EffortProfile(level, identifier),
            repository.Path,
            operationPermissionProfile: operationProfile);
}

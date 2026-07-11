using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Models;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Cli.Services.Agents;

/// <summary>
/// The two codex session postures copied verbatim from RepositoryOrchestrator.BuildOperationalSpec /
/// BuildDecisionSpec. RepositoryId namespaces the session registry; WorkingDirectory is the repo dir.
/// </summary>
internal static class AgentSpecs
{
    public static AgentSessionSpec PlanAuthoring(Repository repository, BrainConfiguration brain) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path);

    public static AgentSessionSpec Review(Repository repository, BrainConfiguration brain) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path);

    // sandboxIdentifier is the codex sandbox mode. Ordinary operational sessions default to
    // "danger-full-access" because Windows sandboxed writes and child-process reads are not reliable enough for
    // one-turn artifact production. Callers may still request a narrower posture explicitly.
    public static AgentSessionSpec BrainOperational(
        Repository repository,
        BrainConfiguration brain,
        string? workingDirectory = null,
        string sandboxIdentifier = "danger-full-access") =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile(
                sandboxIdentifier,
                CanWriteWorkspace: true,
                CanAccessNetwork: sandboxIdentifier == "danger-full-access",
                RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            workingDirectory ?? repository.Path);

    public static AgentSessionSpec Execution(
        Repository repository,
        ValidatedExecutionRecommendation recommendation) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile(
                "danger-full-access",
                CanWriteWorkspace: true,
                CanAccessNetwork: true,
                RequiresApproval: false),
            recommendation.Model,
            recommendation.Effort,
            AgentConfigurationAuthority.Execution,
            repository.Path);

    public static AgentSessionSpec Decision(
        Repository repository,
        BrainConfiguration brain,
        string? resumeThreadId = null) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path,
            resumeThreadId: resumeThreadId);

    public static AgentSessionSpec ScopedArtifactOperation(
        Repository repository,
        BrainConfiguration brain,
        OperationPermissionProfile operationProfile) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile("danger-full-access", CanWriteWorkspace: true, CanAccessNetwork: true, RequiresApproval: false),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path,
            operationPermissionProfile: operationProfile);
}

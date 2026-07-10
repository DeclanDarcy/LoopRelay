using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Configuration;

namespace LoopRelay.Plan.Cli.Services.Cli;

/// <summary>
/// The three codex session postures the planning pipeline needs, per the plan's Process Model table:
/// a danger-full-access authoring session (WritePlan/RevisePlan need to explore the codebase — sandboxed
/// codex cannot spawn child processes on Windows), a read-only zero-permission review session, and a
/// read-only approval-gated posture for scoped artifact operations.
/// All three use SessionRole.Planning and the selected Brain configuration.
/// </summary>
internal static class AgentSpecs
{
    // Retired CLI compatibility paths load the selected settings document explicitly. The unified CLI injects
    // one BrainConfiguration at composition and does not use these overloads.
    public static AgentSessionSpec PlanAuthoring(Repository repository) =>
        PlanAuthoring(repository, CliSettingsLoader.Load().Brain);

    public static AgentSessionSpec Review(Repository repository) =>
        Review(repository, CliSettingsLoader.Load().Brain);

    public static AgentSessionSpec ScopedArtifactOperation(
        Repository repository,
        OperationPermissionProfile operationProfile) =>
        ScopedArtifactOperation(repository, CliSettingsLoader.Load().Brain, operationProfile);

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

    public static AgentSessionSpec ScopedArtifactOperation(
        Repository repository,
        BrainConfiguration brain,
        OperationPermissionProfile operationProfile) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: true),
            brain.Model,
            brain.Effort,
            AgentConfigurationAuthority.Brain,
            repository.Path,
            operationPermissionProfile: operationProfile);
}

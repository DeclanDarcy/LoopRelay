using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Cli;

/// <summary>
/// The two codex session postures copied verbatim from RepositoryOrchestrator.BuildOperationalSpec /
/// BuildDecisionSpec. RepositoryId namespaces the session registry; WorkingDirectory is the repo dir.
/// </summary>
internal static class AgentSpecs
{
    // sandboxIdentifier is the codex sandbox mode. It defaults to "workspace-write" (the context-update
    // evolution one-shot's posture); the execution session overrides it to "danger-full-access" so codex runs
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
            // Stage 2: a Transfer's evolution one-shot passes a sandbox root so codex --cd scopes it there.
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
}

using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Trust;

public enum WorkspaceAuthority
{
    ReadOnly,
    WorkspaceWrite,
    FullAccess,
}

public enum NetworkAuthority
{
    Denied,
    Allowed,
}

public enum ApprovalAuthority
{
    Never,
    OnRequest,
}

public enum ExecutionAuthority
{
    OneShot,
    PersistentSession,
}

public sealed record TrustPolicy(
    string SandboxIdentifier,
    WorkspaceAuthority Workspace,
    NetworkAuthority Network,
    ApprovalAuthority Approval,
    ExecutionAuthority Execution)
{
    public static TrustPolicy FromSandboxProfile(
        SandboxProfile sandbox,
        ExecutionAuthority execution = ExecutionAuthority.PersistentSession)
    {
        WorkspaceAuthority workspace = sandbox.Identifier == "danger-full-access"
            ? WorkspaceAuthority.FullAccess
            : sandbox.CanWriteWorkspace
                ? WorkspaceAuthority.WorkspaceWrite
                : WorkspaceAuthority.ReadOnly;

        return new TrustPolicy(
            sandbox.Identifier,
            workspace,
            sandbox.CanAccessNetwork ? NetworkAuthority.Allowed : NetworkAuthority.Denied,
            sandbox.RequiresApproval ? ApprovalAuthority.OnRequest : ApprovalAuthority.Never,
            execution);
    }

    public TrustPolicyEvidence ToEvidence() =>
        new(
            SandboxIdentifier,
            Workspace.ToString(),
            Network.ToString(),
            Approval.ToString(),
            Execution.ToString());
}

public sealed record TrustPolicyEvidence(
    string Sandbox,
    string Workspace,
    string Network,
    string Approval,
    string Execution);

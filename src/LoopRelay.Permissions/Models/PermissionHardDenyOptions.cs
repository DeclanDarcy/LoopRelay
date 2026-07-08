using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed class PermissionHardDenyOptions
{
    internal PermissionHardDenyOptions(
        IReadOnlySet<string> privilegeEscalationCommands,
        RecursiveForceDeleteOptions recursiveForceDelete,
        IReadOnlySet<string> systemControlCommands,
        IReadOnlySet<string> networkFetchCommands,
        IReadOnlySet<string> gitForcePushFlags,
        IndirectShellExecutionOptions indirectShellExecution)
    {
        PrivilegeEscalationCommands = privilegeEscalationCommands;
        RecursiveForceDelete = recursiveForceDelete;
        SystemControlCommands = systemControlCommands;
        NetworkFetchCommands = networkFetchCommands;
        GitForcePushFlags = gitForcePushFlags;
        IndirectShellExecution = indirectShellExecution;
    }

    public IReadOnlySet<string> PrivilegeEscalationCommands { get; }

    public RecursiveForceDeleteOptions RecursiveForceDelete { get; }

    public IReadOnlySet<string> SystemControlCommands { get; }

    public IReadOnlySet<string> NetworkFetchCommands { get; }

    public IReadOnlySet<string> GitForcePushFlags { get; }

    public IndirectShellExecutionOptions IndirectShellExecution { get; }
}

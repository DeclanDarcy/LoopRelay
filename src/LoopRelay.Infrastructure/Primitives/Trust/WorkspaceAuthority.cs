using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Trust;

public enum WorkspaceAuthority
{
    ReadOnly,
    WorkspaceWrite,
    FullAccess,
}

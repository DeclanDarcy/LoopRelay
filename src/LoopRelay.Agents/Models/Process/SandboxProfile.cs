namespace LoopRelay.Agents.Models;

public sealed record SandboxProfile(
    string Identifier,
    bool CanWriteWorkspace,
    bool CanAccessNetwork,
    bool RequiresApproval);

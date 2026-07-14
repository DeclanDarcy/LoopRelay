namespace LoopRelay.Agents.Models.Process;

public sealed record SandboxProfile(
    string Identifier,
    bool CanWriteWorkspace,
    bool CanAccessNetwork,
    bool RequiresApproval);

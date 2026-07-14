namespace LoopRelay.Infrastructure.Models.Trust;

public sealed record TrustPolicyEvidence(
    string Sandbox,
    string Workspace,
    string Network,
    string Approval,
    string Execution);

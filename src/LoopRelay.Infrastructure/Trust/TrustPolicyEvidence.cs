using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Trust;

public sealed record TrustPolicyEvidence(
    string Sandbox,
    string Workspace,
    string Network,
    string Approval,
    string Execution);

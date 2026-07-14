using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Policy;

public enum AgentRole
{
    RoadmapPlanning,
    EvaluationPlanning,
    PlanAuthoring,
    AdversarialReview,
    ScopedArtifactAuthoring,
    ExecutionDecision,
    Implementation,
    Projection,
    NonImplementationReview,
    CompletionCertification,
}

public sealed record ResolvedAgentRolePolicy(
    string Identity,
    PolicyIdentity OperationalPolicy,
    RuntimeProfileIdentity RuntimeProfile,
    BrainConfiguration Brain,
    IReadOnlyDictionary<AgentRole, string> SessionPolicies,
    string Provenance)
{
    public static ResolvedAgentRolePolicy Create(
        PolicyIdentity policy,
        RuntimeProfileIdentity runtimeProfile,
        BrainConfiguration brain,
        string provenance)
    {
        IReadOnlyDictionary<AgentRole, string> roles = Enum.GetValues<AgentRole>()
            .ToDictionary(role => role, role => role switch
            {
                AgentRole.PlanAuthoring or AgentRole.ExecutionDecision or AgentRole.Implementation => "warm-session",
                AgentRole.ScopedArtifactAuthoring => "scoped-artifact-operation",
                AgentRole.AdversarialReview or AgentRole.NonImplementationReview => "read-only-one-shot",
                _ => "one-shot",
            });
        string material = $"{policy.Value}\n{runtimeProfile.Value}\n{brain.Model}\n{brain.Effort}\n" +
            string.Join("\n", roles.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"));
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return new($"agent_role_policy_{hash[..32]}", policy, runtimeProfile, brain, roles, provenance);
    }
}

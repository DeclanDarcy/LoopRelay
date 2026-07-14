using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Policy;

public sealed record ExecutionRecommendationPolicy(
    bool UseRecommendations,
    IReadOnlySet<AgentModel> AllowedModels,
    AgentEffort MaximumEffort,
    IReadOnlySet<string> SupportedSchemas);

public sealed record ExecutionRecommendationEvaluationRequest(
    ExecutionRecommendationEvidence? Recommendation,
    DecisionProductVersionIdentity CurrentDecisionProduct,
    PolicyIdentity Policy,
    ProviderCapabilityEvidence ProviderCapabilities,
    ResolvedRuntimeProfile FallbackProfile,
    ExecutionRecommendationPolicy RecommendationPolicy,
    string? InvalidReason = null);

/// <summary>
/// Deterministic Policy Authority evaluator. Recommendations influence only model and effort;
/// every other runtime-profile field comes exclusively from the governed fallback profile.
/// </summary>
public sealed class ExecutionRecommendationPolicyEvaluator
{
    public RuntimeProfileEvaluation Evaluate(ExecutionRecommendationEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ProviderCapabilities);
        ArgumentNullException.ThrowIfNull(request.FallbackProfile);
        ArgumentNullException.ThrowIfNull(request.RecommendationPolicy);

        ExecutionRecommendationEvidence? recommendation = request.Recommendation;
        RuntimeProfileEvaluationOutcome outcome;
        AgentModel model = request.FallbackProfile.Model;
        AgentEffort effort = request.FallbackProfile.Effort;
        List<string> reasons = [];

        if (!string.IsNullOrWhiteSpace(request.InvalidReason))
        {
            outcome = RuntimeProfileEvaluationOutcome.Invalid;
            reasons.Add($"Recommendation input was invalid: {request.InvalidReason}");
        }
        else if (recommendation is null)
        {
            outcome = RuntimeProfileEvaluationOutcome.Rejected;
            reasons.Add("No recommendation evidence was available; policy selected the fallback profile.");
        }
        else if (!request.RecommendationPolicy.UseRecommendations)
        {
            outcome = RuntimeProfileEvaluationOutcome.IgnoredByPolicy;
            reasons.Add("Policy is configured to ignore execution recommendations.");
        }
        else if (recommendation.DecisionProduct != request.CurrentDecisionProduct)
        {
            outcome = RuntimeProfileEvaluationOutcome.Stale;
            reasons.Add("Recommendation is bound to a different decision product version.");
        }
        else if (!request.RecommendationPolicy.SupportedSchemas.Contains(recommendation.SchemaVersion))
        {
            outcome = RuntimeProfileEvaluationOutcome.Unsupported;
            reasons.Add($"Recommendation schema '{recommendation.SchemaVersion}' is unsupported.");
        }
        else if (!request.RecommendationPolicy.AllowedModels.Contains(recommendation.RecommendedModel) ||
                 !request.ProviderCapabilities.AvailableModels.Contains(recommendation.RecommendedModel))
        {
            outcome = RuntimeProfileEvaluationOutcome.Rejected;
            reasons.Add("Recommended model is unavailable or disallowed; policy selected the fallback model.");
        }
        else
        {
            model = recommendation.RecommendedModel;
            AgentEffort ceiling = (AgentEffort)Math.Min(
                (int)request.RecommendationPolicy.MaximumEffort,
                (int)request.ProviderCapabilities.MaximumEffort);
            effort = (AgentEffort)Math.Min((int)recommendation.RecommendedEffort, (int)ceiling);
            outcome = effort == recommendation.RecommendedEffort
                ? RuntimeProfileEvaluationOutcome.Accepted
                : RuntimeProfileEvaluationOutcome.Constrained;
            reasons.Add(outcome == RuntimeProfileEvaluationOutcome.Accepted
                ? "Recommended model and effort are allowed by policy and provider capabilities."
                : $"Recommended effort was constrained to {AgentConfigurationCatalog.Format(effort)}.");
        }

        ResolvedRuntimeProfile effective = request.FallbackProfile with
        {
            Identity = ProfileIdentity(
                request.Policy,
                request.ProviderCapabilities.Identity,
                recommendation?.Identity,
                model,
                effort,
                request.FallbackProfile),
            Model = model,
            Effort = effort,
        };
        return new RuntimeProfileEvaluation(
            RuntimeProfileEvaluationIdentity.New(),
            recommendation?.Identity,
            request.CurrentDecisionProduct,
            request.Policy,
            request.ProviderCapabilities,
            outcome,
            effective,
            reasons,
            DateTimeOffset.UtcNow);
    }

    private static RuntimeProfileIdentity ProfileIdentity(
        PolicyIdentity policy,
        ProviderCapabilityEvidenceIdentity capabilities,
        ExecutionRecommendationIdentity? recommendation,
        AgentModel model,
        AgentEffort effort,
        ResolvedRuntimeProfile baseline)
    {
        string canonical = string.Join('|',
            policy.Value, capabilities.Value, recommendation?.Value ?? "none",
            model, effort, baseline.Provider, baseline.ExecutionMode, baseline.SandboxProfile,
            baseline.PermissionProfile, baseline.ApprovalPolicy, baseline.ContinuityPolicy,
            baseline.Timeout.Ticks, baseline.UsagePolicy, baseline.RecoveryPolicy);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new RuntimeProfileIdentity($"runtime_{hash[..32]}");
    }
}

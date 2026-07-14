using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Tests.Policy;

public sealed class ExecutionRecommendationPolicyEvaluatorTests
{
    [Fact]
    public void AcceptsAllowedRecommendationWithoutChangingGovernedProfileFields()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        ResolvedRuntimeProfile fallback = Fallback();

        RuntimeProfileEvaluation evaluation = Evaluate(
            Recommendation(decision, AgentModel.Gpt56Terra, AgentEffort.High),
            decision,
            fallback);

        Assert.Equal(RuntimeProfileEvaluationOutcome.Accepted, evaluation.Outcome);
        Assert.Equal(AgentModel.Gpt56Terra, evaluation.EffectiveProfile.Model);
        Assert.Equal(AgentEffort.High, evaluation.EffectiveProfile.Effort);
        Assert.Equal(fallback.SandboxProfile, evaluation.EffectiveProfile.SandboxProfile);
        Assert.Equal(fallback.PermissionProfile, evaluation.EffectiveProfile.PermissionProfile);
    }

    [Fact]
    public void ConstrainsEffortAbovePolicyCeiling()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        RuntimeProfileEvaluation evaluation = Evaluate(
            Recommendation(decision, AgentModel.Gpt56Terra, AgentEffort.XHigh),
            decision,
            Fallback(),
            maximumEffort: AgentEffort.Medium);

        Assert.Equal(RuntimeProfileEvaluationOutcome.Constrained, evaluation.Outcome);
        Assert.Equal(AgentEffort.Medium, evaluation.EffectiveProfile.Effort);
    }

    [Fact]
    public void RejectsUnavailableModelAndUsesFallback()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        ResolvedRuntimeProfile fallback = Fallback();
        RuntimeProfileEvaluation evaluation = Evaluate(
            Recommendation(decision, AgentModel.Gpt53CodexSpark, AgentEffort.Low),
            decision,
            fallback,
            availableModels: [AgentModel.Gpt56Sol]);

        Assert.Equal(RuntimeProfileEvaluationOutcome.Rejected, evaluation.Outcome);
        Assert.Equal(fallback.Model, evaluation.EffectiveProfile.Model);
    }

    [Fact]
    public void DifferentDecisionProductMakesRecommendationStaleRegardlessOfTimestamp()
    {
        ExecutionRecommendationEvidence recommendation = Recommendation(
            DecisionProductVersionIdentity.New(), AgentModel.Gpt56Terra, AgentEffort.High);

        RuntimeProfileEvaluation evaluation = Evaluate(
            recommendation,
            DecisionProductVersionIdentity.New(),
            Fallback());

        Assert.Equal(RuntimeProfileEvaluationOutcome.Stale, evaluation.Outcome);
    }

    [Fact]
    public void PolicyCanIgnoreRecommendationDeterministically()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        RuntimeProfileEvaluation evaluation = Evaluate(
            Recommendation(decision, AgentModel.Gpt56Terra, AgentEffort.High),
            decision,
            Fallback(),
            useRecommendations: false);

        Assert.Equal(RuntimeProfileEvaluationOutcome.IgnoredByPolicy, evaluation.Outcome);
    }

    [Fact]
    public void MalformedRecommendationProducesDurableInvalidDecisionWithFallback()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        ResolvedRuntimeProfile fallback = Fallback();
        var request = new ExecutionRecommendationEvaluationRequest(
            null,
            decision,
            new PolicyIdentity("policy-test"),
            new ProviderCapabilityEvidence(
                ProviderCapabilityEvidenceIdentity.New(), "codex", Enum.GetValues<AgentModel>(),
                AgentEffort.XHigh, DateTimeOffset.UtcNow),
            fallback,
            new ExecutionRecommendationPolicy(
                true,
                new HashSet<AgentModel>(Enum.GetValues<AgentModel>()),
                AgentEffort.High,
                new HashSet<string> { ExecutionRecommendationEvidenceSchemas.Version1 }),
            "unknown model");

        RuntimeProfileEvaluation evaluation = new ExecutionRecommendationPolicyEvaluator().Evaluate(request);

        Assert.Equal(RuntimeProfileEvaluationOutcome.Invalid, evaluation.Outcome);
        Assert.Equal(fallback.Model, evaluation.EffectiveProfile.Model);
    }

    private static RuntimeProfileEvaluation Evaluate(
        ExecutionRecommendationEvidence? recommendation,
        DecisionProductVersionIdentity decision,
        ResolvedRuntimeProfile fallback,
        AgentEffort maximumEffort = AgentEffort.High,
        IReadOnlyList<AgentModel>? availableModels = null,
        bool useRecommendations = true) =>
        new ExecutionRecommendationPolicyEvaluator().Evaluate(new ExecutionRecommendationEvaluationRequest(
            recommendation,
            decision,
            new PolicyIdentity("policy-test"),
            new ProviderCapabilityEvidence(
                ProviderCapabilityEvidenceIdentity.New(),
                "codex",
                availableModels ?? Enum.GetValues<AgentModel>(),
                AgentEffort.XHigh,
                DateTimeOffset.UtcNow),
            fallback,
            new ExecutionRecommendationPolicy(
                useRecommendations,
                new HashSet<AgentModel>(Enum.GetValues<AgentModel>()),
                maximumEffort,
                new HashSet<string> { ExecutionRecommendationEvidenceSchemas.Version1 })));

    private static ExecutionRecommendationEvidence Recommendation(
        DecisionProductVersionIdentity decision,
        AgentModel model,
        AgentEffort effort) =>
        new(
            ExecutionRecommendationIdentity.New(),
            decision,
            new CanonicalCausalContext(
                WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
                TransitionRunIdentity.New(), AttemptIdentity.New()),
            AgentSessionIdentity.New(),
            TurnIdentity.New(),
            model,
            effort,
            "Agent advisory recommendation.",
            DateTimeOffset.UtcNow);

    private static ResolvedRuntimeProfile Fallback() => new(
        new RuntimeProfileIdentity("runtime-fallback"),
        "codex",
        AgentModel.Gpt56Sol,
        AgentEffort.Medium,
        "persistent-session",
        "danger-full-access",
        "execution-default",
        "never",
        "resume-or-recover",
        TimeSpan.FromMinutes(30),
        "default",
        "reconcile-before-retry");
}

using LoopRelay.Roadmap.Cli;
using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class CompletionCertificationPolicyTests
{
    [Theory]
    [InlineData("Fully Complete", "None", "Close Epic", "UpdateRoadmapCompletionContext")]
    [InlineData("Functionally Complete", "Mixed", "Close With Follow-Up", "UpdateRoadmapCompletionContext")]
    [InlineData("Partially Complete", "Negative", "Continue Epic", "ContinueExecution")]
    [InlineData("Not Complete", "None", "Continue Epic", "ContinueExecution")]
    [InlineData("Inconclusive", "Unknown", "Gather More Evidence", "GatherAdditionalEvidence")]
    [InlineData("Functionally Complete", "Negative", "Reopen Epic", "ReturnToEpicPreparationAudit")]
    public void Certification_policy_accepts_coherent_decisions(
        string completionStatus,
        string driftClassification,
        string recommendation,
        string expectedIntent)
    {
        var policy = new CompletionCertificationPolicy();

        CompletionCertificationPolicyResult result = policy.Validate(new CompletionEvaluationDecision(
            completionStatus,
            driftClassification,
            recommendation));

        Assert.True(result.IsValid, result.RejectionReason);
        Assert.NotNull(result.Rule);
        Assert.Equal(recommendation, result.Rule!.ClosureRecommendation);

        CompletionCertificationRoute route = new CompletionCertificationRouter().Route(result.Decision);
        Assert.Equal(expectedIntent, route.Intent.ToString());
        Assert.Equal(recommendation, route.ClosureRecommendation);
    }

    [Theory]
    [InlineData("Not Complete", "None", "Close Epic", "does not allow completion status `Not Complete`")]
    [InlineData("Partially Complete", "Positive", "Close Epic", "does not allow completion status `Partially Complete`")]
    [InlineData("Inconclusive", "Unknown", "Close With Follow-Up", "does not allow completion status `Inconclusive`")]
    [InlineData("Fully Complete", "Unknown", "Close Epic", "does not allow drift classification `Unknown`")]
    [InlineData("Fully Complete", "None", "Continue Epic", "does not allow completion status `Fully Complete`")]
    public void Certification_policy_rejects_contradictory_decisions(
        string completionStatus,
        string driftClassification,
        string recommendation,
        string expectedReason)
    {
        var policy = new CompletionCertificationPolicy();

        CompletionCertificationPolicyResult result = policy.Validate(new CompletionEvaluationDecision(
            completionStatus,
            driftClassification,
            recommendation));

        Assert.False(result.IsValid);
        Assert.Null(result.Rule);
        Assert.NotNull(result.RejectionReason);
        Assert.Contains(expectedReason, result.RejectionReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Certification_policy_requires_coverage_for_new_completion_status_values()
    {
        var policy = new CompletionCertificationPolicy();
        string[] extendedStatuses = [.. CompletionEvaluationParser.AllowedCompletionStatuses, "Deferred"];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompletionCertificationPolicy(
                policy.Rules,
                extendedStatuses,
                CompletionEvaluationParser.AllowedDriftClassifications,
                CompletionCertificationRouter.AllowedRecommendations));

        Assert.Contains("Deferred", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Certification_policy_requires_coverage_for_new_drift_values()
    {
        var policy = new CompletionCertificationPolicy();
        string[] extendedDrift = [.. CompletionEvaluationParser.AllowedDriftClassifications, "Ambiguous"];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompletionCertificationPolicy(
                policy.Rules,
                CompletionEvaluationParser.AllowedCompletionStatuses,
                extendedDrift,
                CompletionCertificationRouter.AllowedRecommendations));

        Assert.Contains("Ambiguous", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Certification_policy_requires_coverage_for_new_recommendation_values()
    {
        var policy = new CompletionCertificationPolicy();
        string[] extendedRecommendations = [.. CompletionCertificationVocabulary.ClosureRecommendations, "Suspend Epic"];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompletionCertificationPolicy(
                policy.Rules,
                CompletionCertificationVocabulary.CompletionStatuses,
                CompletionCertificationVocabulary.DriftClassifications,
                extendedRecommendations));

        Assert.Contains("Suspend Epic", exception.Message, StringComparison.Ordinal);
    }
}

using LoopRelay.Completion.Models.Certification;

namespace LoopRelay.Completion.Services.Certification;

public sealed class CompletionCertificationPolicy
{
    private static readonly string[] CoveredCompletionStatuses =
    [
        "Fully Complete",
        "Functionally Complete",
        "Partially Complete",
        "Not Complete",
        "Inconclusive",
    ];

    private static readonly string[] CoveredDriftClassifications =
    [
        "None",
        "Positive",
        "Negative",
        "Mixed",
        "Unknown",
    ];

    private static readonly CompletionCertificationPolicyRule[] DefaultRules =
    [
        new(
            "Close Epic",
            ["Fully Complete", "Functionally Complete"],
            ["None", "Positive"],
            "Epic closure requires a completed status and non-blocking drift."),
        new(
            "Close With Follow-Up",
            ["Fully Complete", "Functionally Complete"],
            ["None", "Positive", "Mixed"],
            "Closure with follow-up requires a completed status; mixed drift is allowed only as non-blocking follow-up."),
        new(
            "Continue Epic",
            ["Partially Complete", "Not Complete"],
            ["None", "Positive", "Negative", "Mixed"],
            "Continuation requires remaining implementation work with determinate drift."),
        new(
            "Reopen Epic",
            ["Fully Complete", "Functionally Complete", "Partially Complete", "Not Complete", "Inconclusive"],
            ["Negative", "Mixed", "Unknown"],
            "Reopening is reserved for negative, mixed, or unknown drift that needs renewed preparation or audit."),
        new(
            "Gather More Evidence",
            ["Inconclusive"],
            ["Negative", "Mixed", "Unknown"],
            "Evidence gathering requires an inconclusive certification with unresolved drift or evidence quality."),
    ];

    private readonly IReadOnlyDictionary<string, CompletionCertificationPolicyRule> rules;
    private readonly IReadOnlySet<string> allowedCompletionStatuses;
    private readonly IReadOnlySet<string> allowedDriftClassifications;
    private readonly IReadOnlySet<string> allowedRecommendations;

    public CompletionCertificationPolicy()
        : this(
            DefaultRules,
            CompletionCertificationVocabulary.CompletionStatuses,
            CompletionCertificationVocabulary.DriftClassifications,
            CompletionCertificationVocabulary.ClosureRecommendations)
    {
    }

    public CompletionCertificationPolicy(
        IEnumerable<CompletionCertificationPolicyRule> rules,
        IReadOnlyList<string> allowedCompletionStatuses,
        IReadOnlyList<string> allowedDriftClassifications,
        IReadOnlyList<string> allowedRecommendations)
    {
        this.rules = rules.ToDictionary(rule => rule.ClosureRecommendation, StringComparer.Ordinal);
        this.allowedCompletionStatuses = allowedCompletionStatuses.ToHashSet(StringComparer.Ordinal);
        this.allowedDriftClassifications = allowedDriftClassifications.ToHashSet(StringComparer.Ordinal);
        this.allowedRecommendations = allowedRecommendations.ToHashSet(StringComparer.Ordinal);

        EnsureVocabularyCoverage("completion statuses", CoveredCompletionStatuses, allowedCompletionStatuses);
        EnsureVocabularyCoverage("drift classifications", CoveredDriftClassifications, allowedDriftClassifications);
        EnsureRecommendationCoverage(allowedRecommendations);
        EnsureRuleVocabulary();
    }

    public IReadOnlyCollection<CompletionCertificationPolicyRule> Rules => this.rules.Values.ToList();

    public CompletionCertificationPolicyResult Validate(CompletionEvaluationDecision decision)
    {
        if (!this.allowedCompletionStatuses.Contains(decision.OverallCompletionStatus))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Completion status `{decision.OverallCompletionStatus}` is not covered by completion certification policy.");
        }

        if (!this.allowedDriftClassifications.Contains(decision.OverallDriftClassification))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Drift classification `{decision.OverallDriftClassification}` is not covered by completion certification policy.");
        }

        if (!this.allowedRecommendations.Contains(decision.ClosureRecommendation))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Closure recommendation `{decision.ClosureRecommendation}` is not covered by completion certification policy.");
        }

        if (!this.rules.TryGetValue(decision.ClosureRecommendation, out CompletionCertificationPolicyRule? rule))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Closure recommendation `{decision.ClosureRecommendation}` has no completion certification policy rule.");
        }

        if (!rule.AllowsCompletionStatus(decision.OverallCompletionStatus))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Closure recommendation `{decision.ClosureRecommendation}` does not allow completion status `{decision.OverallCompletionStatus}`. Allowed statuses: {string.Join(", ", rule.AllowedCompletionStatuses)}.");
        }

        if (!rule.AllowsDriftClassification(decision.OverallDriftClassification))
        {
            return CompletionCertificationPolicyResult.Invalid(
                decision,
                $"Closure recommendation `{decision.ClosureRecommendation}` does not allow drift classification `{decision.OverallDriftClassification}`. Allowed drift classifications: {string.Join(", ", rule.AllowedDriftClassifications)}.");
        }

        return CompletionCertificationPolicyResult.Valid(decision, rule);
    }

    private static void EnsureVocabularyCoverage(
        string label,
        IReadOnlyList<string> covered,
        IReadOnlyList<string> allowed)
    {
        HashSet<string> coveredSet = covered.ToHashSet(StringComparer.Ordinal);
        string[] missing = allowed
            .Where(value => !coveredSet.Contains(value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Completion certification policy is missing {label}: {string.Join(", ", missing)}");
        }
    }

    private void EnsureRecommendationCoverage(IReadOnlyList<string> allowedRecommendations)
    {
        string[] missing = allowedRecommendations
            .Where(recommendation => !this.rules.ContainsKey(recommendation))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException("Completion certification policy is missing recommendations: " + string.Join(", ", missing));
        }
    }

    private void EnsureRuleVocabulary()
    {
        foreach (CompletionCertificationPolicyRule rule in this.rules.Values)
        {
            if (!this.allowedRecommendations.Contains(rule.ClosureRecommendation))
            {
                throw new InvalidOperationException($"Completion certification policy rule uses unsupported recommendation `{rule.ClosureRecommendation}`.");
            }

            string[] unsupportedStatuses = rule.AllowedCompletionStatuses
                .Where(status => !this.allowedCompletionStatuses.Contains(status))
                .ToArray();
            if (unsupportedStatuses.Length > 0)
            {
                throw new InvalidOperationException($"Completion certification policy rule `{rule.ClosureRecommendation}` uses unsupported completion statuses: {string.Join(", ", unsupportedStatuses)}");
            }

            string[] unsupportedDrift = rule.AllowedDriftClassifications
                .Where(drift => !this.allowedDriftClassifications.Contains(drift))
                .ToArray();
            if (unsupportedDrift.Length > 0)
            {
                throw new InvalidOperationException($"Completion certification policy rule `{rule.ClosureRecommendation}` uses unsupported drift classifications: {string.Join(", ", unsupportedDrift)}");
            }
        }
    }
}

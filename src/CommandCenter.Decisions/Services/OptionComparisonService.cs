using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class OptionComparisonService : IOptionComparisonService
{
    public IReadOnlyList<DecisionTradeoffComparison> CompareOptions(
        DecisionCandidate candidate,
        IReadOnlyList<AnalyzedDecisionOption> analyzedOptions,
        IReadOnlyList<DecisionOptionRelationship> relationships,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        var comparisons = new List<DecisionTradeoffComparison>();
        DecisionEvidence[] comparisonEvidence = EvidenceForComparison(candidate, evidence);

        foreach (AnalyzedDecisionOption option in analyzedOptions.OrderBy(option => option.OptionId, StringComparer.Ordinal))
        {
            DecisionOptionRelationship[] related = relationships
                .Where(relationship => relationship.SourceOptionId == option.OptionId ||
                    relationship.TargetOptionId == option.OptionId)
                .ToArray();
            bool hasConflict = related.Any(relationship => relationship.Type == DecisionOptionRelationshipType.ConflictsWith);
            bool hasDependency = related.Any(relationship => relationship.Type == DecisionOptionRelationshipType.DependsOn);
            string strongestBenefit = option.Benefits
                .OrderByDescending(benefit => benefit.Impact)
                .Select(benefit => benefit.Statement)
                .FirstOrDefault() ?? "No generated benefit.";
            string highestRisk = option.Risks
                .OrderByDescending(risk => risk.Severity)
                .Select(risk => risk.Statement)
                .FirstOrDefault() ?? "No generated risk.";
            string[] disqualifiers = DisqualifyingConstraints(candidate, option, hasConflict);

            comparisons.Add(new DecisionTradeoffComparison(
                option.OptionId,
                [$"Relative strength: {strongestBenefit}"],
                [$"Relative weakness: {option.Costs.FirstOrDefault()?.Statement ?? "No generated cost."}"],
                UniqueAdvantages(option, hasDependency),
                UniqueRisks(option, highestRisk, hasConflict),
                disqualifiers,
                comparisonEvidence));
        }

        return comparisons.ToArray();
    }

    private static string[] UniqueAdvantages(AnalyzedDecisionOption option, bool hasDependency)
    {
        var advantages = new List<string>();
        if (option.Risks.Any(risk => risk.IsUnknown))
        {
            advantages.Add("Makes unknown downstream impact explicit for reviewer comparison.");
        }

        if (hasDependency)
        {
            advantages.Add("Clarifies sequencing dependencies relative to another generated option.");
        }

        if (advantages.Count == 0)
        {
            advantages.Add("Provides a direct comparison point against other generated options.");
        }

        return advantages.Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] UniqueRisks(
        AnalyzedDecisionOption option,
        string highestRisk,
        bool hasConflict)
    {
        var risks = new List<string> { highestRisk };
        if (hasConflict)
        {
            risks.Add("Conflicts with at least one alternative option and requires human review before execution guidance.");
        }

        return risks.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] DisqualifyingConstraints(
        DecisionCandidate candidate,
        AnalyzedDecisionOption option,
        bool hasConflict)
    {
        bool constraintCandidate = candidate.Signals.Any(signal =>
            signal.Kind.Contains("Constraint", StringComparison.OrdinalIgnoreCase));
        if (!constraintCandidate || !hasConflict)
        {
            return [];
        }

        return [$"{option.OptionId} participates in a constraint conflict; it is not execution guidance until explicitly resolved by a human."];
    }

    private static DecisionEvidence[] EvidenceForComparison(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        return evidence
            .Where(item => item.Sources.Count == 0 ||
                item.Sources.Any(source => source.CandidateId == candidate.Id || source.RelativePath is not null))
            .OrderBy(item => item.Summary, StringComparer.Ordinal)
            .Take(4)
            .ToArray();
    }
}

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
            AnalyzedDecisionOption[] alternatives = analyzedOptions
                .Where(alternative => alternative.OptionId != option.OptionId)
                .OrderBy(alternative => alternative.OptionId, StringComparer.Ordinal)
                .ToArray();
            DecisionOptionRelationship[] related = relationships
                .Where(relationship => relationship.SourceOptionId == option.OptionId ||
                    relationship.TargetOptionId == option.OptionId)
                .ToArray();
            bool hasConflict = related.Any(relationship => relationship.Type == DecisionOptionRelationshipType.ConflictsWith);
            bool hasDependency = related.Any(relationship => relationship.Type == DecisionOptionRelationshipType.DependsOn);
            string[] disqualifiers = DisqualifyingConstraints(candidate, option, hasConflict);

            comparisons.Add(new DecisionTradeoffComparison(
                option.OptionId,
                RelativeStrengths(option, alternatives),
                RelativeWeaknesses(option, alternatives),
                UniqueAdvantages(option, alternatives, hasDependency),
                UniqueRisks(option, alternatives, hasConflict),
                disqualifiers,
                comparisonEvidence));
        }

        return comparisons.ToArray();
    }

    private static string[] RelativeStrengths(
        AnalyzedDecisionOption option,
        IReadOnlyList<AnalyzedDecisionOption> alternatives)
    {
        var strengths = new List<string>();
        DecisionBenefit? strongestBenefit = option.Benefits
            .OrderByDescending(benefit => benefit.Impact)
            .ThenBy(benefit => benefit.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        TradeoffImpact strongestAlternativeImpact = alternatives
            .SelectMany(alternative => alternative.Benefits)
            .Select(benefit => benefit.Impact)
            .DefaultIfEmpty(TradeoffImpact.Low)
            .Max();

        if (strongestBenefit is not null)
        {
            string comparison = strongestBenefit.Impact > strongestAlternativeImpact
                ? "higher-impact benefit than the alternatives"
                : "primary benefit for reviewer comparison";
            strengths.Add($"Relative strength: {comparison}: {strongestBenefit.Statement}");
        }

        DecisionConsequence? executionConsequence = option.Consequences
            .Where(consequence => consequence.Statement.Contains("Execution", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(consequence => consequence.Impact)
            .ThenBy(consequence => consequence.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        if (executionConsequence is not null)
        {
            strengths.Add($"Execution consequence to compare: {executionConsequence.Statement}");
        }

        return EmptyIfNeeded(strengths, "Relative strength: no generated benefit distinguished this option.");
    }

    private static string[] RelativeWeaknesses(
        AnalyzedDecisionOption option,
        IReadOnlyList<AnalyzedDecisionOption> alternatives)
    {
        var weaknesses = new List<string>();
        DecisionCost? highestCost = option.Costs
            .OrderByDescending(cost => cost.Impact)
            .ThenBy(cost => cost.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        TradeoffImpact strongestAlternativeCost = alternatives
            .SelectMany(alternative => alternative.Costs)
            .Select(cost => cost.Impact)
            .DefaultIfEmpty(TradeoffImpact.Low)
            .Max();

        if (highestCost is not null)
        {
            string comparison = highestCost.Impact >= strongestAlternativeCost
                ? "cost is at least as significant as the alternatives"
                : "lower-impact cost than at least one alternative";
            weaknesses.Add($"Relative weakness: {comparison}: {highestCost.Statement}");
        }

        int dependencyDelta = option.Dependencies.Count - alternatives
            .Select(alternative => alternative.Dependencies.Count)
            .DefaultIfEmpty(0)
            .Min();
        if (dependencyDelta > 0)
        {
            weaknesses.Add(
                $"Dependency load: requires {dependencyDelta} more generated dependency item(s) than the least-dependent alternative.");
        }

        return EmptyIfNeeded(weaknesses, "Relative weakness: no generated cost distinguished this option.");
    }

    private static string[] UniqueAdvantages(
        AnalyzedDecisionOption option,
        IReadOnlyList<AnalyzedDecisionOption> alternatives,
        bool hasDependency)
    {
        var advantages = new List<string>();
        string[] alternativeBenefitStatements = alternatives
            .SelectMany(alternative => alternative.Benefits)
            .Select(benefit => benefit.Statement)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DecisionBenefit? distinctBenefit = option.Benefits
            .Where(benefit => !alternativeBenefitStatements.Contains(benefit.Statement, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(benefit => benefit.Impact)
            .ThenBy(benefit => benefit.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        if (distinctBenefit is not null)
        {
            advantages.Add($"Distinct benefit: {distinctBenefit.Statement}");
        }

        DecisionConsequence? distinctConsequence = option.Consequences
            .Where(consequence => alternatives.All(alternative =>
                alternative.Consequences.All(other =>
                    !string.Equals(other.Statement, consequence.Statement, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(consequence => consequence.Impact)
            .ThenBy(consequence => consequence.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        if (distinctConsequence is not null)
        {
            advantages.Add($"Distinct consequence: {distinctConsequence.Statement}");
        }

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
        IReadOnlyList<AnalyzedDecisionOption> alternatives,
        bool hasConflict)
    {
        var risks = new List<string>();
        string[] alternativeRiskStatements = alternatives
            .SelectMany(alternative => alternative.Risks)
            .Select(risk => risk.Statement)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DecisionRisk? highestRisk = option.Risks
            .OrderByDescending(risk => risk.Severity)
            .ThenByDescending(risk => risk.IsUnknown)
            .ThenBy(risk => risk.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        if (highestRisk is not null)
        {
            string riskKind = alternativeRiskStatements.Contains(highestRisk.Statement, StringComparer.OrdinalIgnoreCase)
                ? "Shared highest risk"
                : "Distinct highest risk";
            risks.Add($"{riskKind}: {highestRisk.Statement}");
        }

        DecisionRisk? unknownRisk = option.Risks
            .Where(risk => risk.IsUnknown)
            .OrderByDescending(risk => risk.Severity)
            .ThenBy(risk => risk.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknownRisk is not null)
        {
            risks.Add($"Unknown risk requiring review: {unknownRisk.Statement}");
        }

        if (hasConflict)
        {
            risks.Add("Conflicts with at least one alternative option and requires human review before execution guidance.");
        }

        return EmptyIfNeeded(risks, "No generated risk distinguished this option.");
    }

    private static string[] DisqualifyingConstraints(
        DecisionCandidate candidate,
        AnalyzedDecisionOption option,
        bool hasConflict)
    {
        string[] contextDisqualifiers = option.Risks
            .Where(risk => risk.Severity == TradeoffSeverity.High &&
                risk.Statement.Contains("Constraint may be violated", StringComparison.OrdinalIgnoreCase))
            .Select(risk => risk.Statement)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool constraintCandidate = candidate.Signals.Any(signal =>
            signal.Kind.Contains("Constraint", StringComparison.OrdinalIgnoreCase));
        if (!constraintCandidate || !hasConflict)
        {
            return contextDisqualifiers;
        }

        return contextDisqualifiers
            .Concat([$"{option.OptionId} participates in a constraint conflict; it is not execution guidance until explicitly resolved by a human."])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] EmptyIfNeeded(IReadOnlyList<string> values, string fallback)
    {
        return values.Count == 0
            ? [fallback]
            : values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
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

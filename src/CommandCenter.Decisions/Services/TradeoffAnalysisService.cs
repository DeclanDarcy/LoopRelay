using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class TradeoffAnalysisService : ITradeoffAnalysisService
{
    public IReadOnlyList<AnalyzedDecisionOption> AnalyzeOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence,
        string contextFingerprint)
    {
        DecisionEvidence[] candidateEvidence = EvidenceForAnalysis(candidate, evidence);
        string signalSummary = SignalSummary(candidate);
        var analyzed = new List<AnalyzedDecisionOption>();

        foreach (DecisionOption option in options.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            DecisionEvidence[] optionEvidence = option.Evidence.Count == 0
                ? candidateEvidence
                : option.Evidence.ToArray();
            DecisionBenefit[] benefits = [
                new(
                    BenefitFor(candidate, option, signalSummary),
                    BenefitImpact(option),
                    optionEvidence)
            ];
            DecisionCost[] costs = [
                new(
                    CostFor(candidate, option),
                    CostImpact(option),
                    optionEvidence)
            ];
            DecisionRisk[] risks = RisksFor(candidate, option, candidateEvidence);
            DecisionDependency[] dependencies = DependenciesFor(option, candidateEvidence);
            DecisionConsequence[] consequences = [
                new(
                    ConsequenceFor(candidate, option),
                    ConsequenceImpact(option),
                    optionEvidence)
            ];
            string[] diagnostics = [
                $"Analyzed {option.Id} as {option.Type} for {candidate.Classification} candidate {candidate.Id}.",
                $"Context fingerprint: {contextFingerprint}."
            ];

            analyzed.Add(new AnalyzedDecisionOption(
                option.Id,
                benefits,
                costs,
                risks,
                dependencies,
                consequences,
                diagnostics,
                optionEvidence));
        }

        return analyzed.ToArray();
    }

    private static DecisionRisk[] RisksFor(
        DecisionCandidate candidate,
        DecisionOption option,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        var risks = new List<DecisionRisk>
        {
            new(
                RiskFor(candidate, option),
                RiskSeverity(candidate, option),
                false,
                evidence)
        };

        if (option.Evidence.Count == 0 ||
            option.Type is DecisionOptionType.Replace or DecisionOptionType.Delay or DecisionOptionType.Investigate or DecisionOptionType.Constrain)
        {
            risks.Add(new DecisionRisk(
                $"Unknown downstream impact remains for {option.Title} because available evidence may not fully cover implementation, migration, or sequencing effects.",
                TradeoffSeverity.Medium,
                true,
                evidence));
        }

        return risks.ToArray();
    }

    private static DecisionDependency[] DependenciesFor(
        DecisionOption option,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        string[] dependencies = option.Dependencies.Count == 0
            ? ["Repository evidence remains available for human review."]
            : option.Dependencies.ToArray();

        return dependencies
            .Order(StringComparer.Ordinal)
            .Select(dependency => new DecisionDependency(dependency, evidence))
            .ToArray();
    }

    private static string BenefitFor(
        DecisionCandidate candidate,
        DecisionOption option,
        string signalSummary)
    {
        return option.Type switch
        {
            DecisionOptionType.Delay =>
                $"Preserves optionality for {candidate.Title} while {signalSummary} remains unsettled.",
            DecisionOptionType.Investigate =>
                $"Improves evidence quality before authority is applied to {candidate.Title}.",
            DecisionOptionType.Preserve =>
                $"Limits disruption while still addressing the {candidate.Classification} candidate.",
            DecisionOptionType.Replace =>
                $"Can remove the contested structure behind {candidate.Title} when the current path is the constraint.",
            DecisionOptionType.Refactor =>
                $"Creates a bounded path to resolve {candidate.Title} without requiring a wholesale replacement.",
            DecisionOptionType.Constrain =>
                $"Narrows scope so {candidate.Title} can proceed with clearer boundaries.",
            DecisionOptionType.Expand =>
                $"Increases commitment where repository evidence already supports the direction for {candidate.Title}.",
            _ =>
                $"Moves {candidate.Title} from promoted candidate evidence toward executable decision guidance."
        };
    }

    private static string CostFor(DecisionCandidate candidate, DecisionOption option)
    {
        return option.Type switch
        {
            DecisionOptionType.Delay =>
                $"Leaves {candidate.Title} unresolved and may keep dependent execution waiting.",
            DecisionOptionType.Investigate =>
                $"Spends another slice gathering evidence before execution can consume a resolved decision.",
            DecisionOptionType.Preserve =>
                $"May carry forward existing constraints that caused {candidate.Title} to surface.",
            DecisionOptionType.Replace =>
                $"Requires migration planning and broader validation before execution can rely on the replacement.",
            DecisionOptionType.Refactor =>
                $"Requires careful scoping to avoid expanding beyond the promoted candidate evidence.",
            DecisionOptionType.Constrain =>
                $"May defer useful breadth from the original candidate summary.",
            DecisionOptionType.Expand =>
                $"Increases near-term commitment and can crowd out lower-priority work.",
            _ =>
                $"Requires human review before {candidate.Title} can become authoritative."
        };
    }

    private static string RiskFor(DecisionCandidate candidate, DecisionOption option)
    {
        if (candidate.Signals.Any(signal => IsConstraintSignal(signal.Kind)) &&
            option.Type is not DecisionOptionType.Refactor)
        {
            return $"Constraint conflict may remain unresolved if {option.Title} does not explicitly remove the contested constraint.";
        }

        return option.Type switch
        {
            DecisionOptionType.Replace =>
                $"Replacement may invalidate assumptions held by prior work around {candidate.Title}.",
            DecisionOptionType.Refactor =>
                $"Refactoring may expose hidden coupling not visible in the current candidate evidence.",
            DecisionOptionType.Delay =>
                $"Deferral may allow the same blocker or missing direction to recur in execution planning.",
            DecisionOptionType.Investigate =>
                $"Investigation may fail to produce enough new evidence for timely resolution.",
            _ =>
                $"Evidence for {candidate.Title} may be current but incomplete for all downstream consequences."
        };
    }

    private static string ConsequenceFor(DecisionCandidate candidate, DecisionOption option)
    {
        return option.Type switch
        {
            DecisionOptionType.Delay or DecisionOptionType.Investigate =>
                $"Execution should treat {candidate.Title} as unresolved until a later human resolution.",
            DecisionOptionType.Replace =>
                $"Execution will need to account for migration and compatibility work if this option is later accepted.",
            DecisionOptionType.Refactor =>
                $"Execution can proceed through a bounded change path if this option is later accepted.",
            _ =>
                $"Execution can consume clearer guidance for {candidate.Title} after human resolution."
        };
    }

    private static TradeoffImpact BenefitImpact(DecisionOption option)
    {
        return option.Type switch
        {
            DecisionOptionType.Replace or DecisionOptionType.Expand => TradeoffImpact.High,
            DecisionOptionType.Delay or DecisionOptionType.Investigate => TradeoffImpact.Medium,
            _ => TradeoffImpact.Medium
        };
    }

    private static TradeoffImpact CostImpact(DecisionOption option)
    {
        return option.Type switch
        {
            DecisionOptionType.Replace => TradeoffImpact.High,
            DecisionOptionType.Delay or DecisionOptionType.Investigate => TradeoffImpact.Medium,
            _ => TradeoffImpact.Medium
        };
    }

    private static TradeoffSeverity RiskSeverity(DecisionCandidate candidate, DecisionOption option)
    {
        if (candidate.Priority == DecisionCandidatePriority.Blocking ||
            option.Type == DecisionOptionType.Replace)
        {
            return TradeoffSeverity.High;
        }

        return option.Type is DecisionOptionType.Delay or DecisionOptionType.Investigate
            ? TradeoffSeverity.Medium
            : TradeoffSeverity.Low;
    }

    private static TradeoffImpact ConsequenceImpact(DecisionOption option)
    {
        return option.Type is DecisionOptionType.Replace or DecisionOptionType.Delay
            ? TradeoffImpact.High
            : TradeoffImpact.Medium;
    }

    private static DecisionEvidence[] EvidenceForAnalysis(
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

    private static string SignalSummary(DecisionCandidate candidate)
    {
        string[] kinds = candidate.Signals
            .Select(signal => signal.Kind)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return kinds.Length == 0 ? "the candidate signal" : string.Join(", ", kinds);
    }

    private static bool IsConstraintSignal(string kind)
    {
        return kind.Contains("Constraint", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Conflict", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Contradiction", StringComparison.OrdinalIgnoreCase);
    }
}

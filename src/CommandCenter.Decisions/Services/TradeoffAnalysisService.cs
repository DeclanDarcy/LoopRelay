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
        DecisionGenerationContext generationContext,
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
            DecisionBenefit[] benefits = BenefitsFor(candidate, option, signalSummary, optionEvidence, generationContext);
            DecisionCost[] costs = CostsFor(candidate, option, optionEvidence, generationContext);
            DecisionRisk[] risks = RisksFor(candidate, option, candidateEvidence, generationContext);
            DecisionDependency[] dependencies = DependenciesFor(option, candidateEvidence, generationContext);
            DecisionConsequence[] consequences = ConsequencesFor(candidate, option, optionEvidence, generationContext);
            string[] diagnostics = DiagnosticsFor(candidate, option, generationContext, contextFingerprint);

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

    private static DecisionBenefit[] BenefitsFor(
        DecisionCandidate candidate,
        DecisionOption option,
        string signalSummary,
        IReadOnlyList<DecisionEvidence> optionEvidence,
        DecisionGenerationContext generationContext)
    {
        var benefits = new List<DecisionBenefit>
        {
            new(
                BenefitFor(candidate, option, signalSummary),
                BenefitImpact(option),
                optionEvidence)
        };

        DecisionGenerationContextEntry? goal = BestMatch(generationContext.Goals, candidate, option);
        if (goal is not null)
        {
            benefits.Add(new DecisionBenefit(
                $"Supports current generation-context goal: {goal.Statement}",
                TradeoffImpact.High,
                goal.Evidence));
        }

        DecisionGenerationContextEntry? repositoryState = BestMatch(generationContext.RepositoryState, candidate, option);
        if (repositoryState is not null)
        {
            benefits.Add(new DecisionBenefit(
                $"Connects the option to repository state: {repositoryState.Statement}",
                TradeoffImpact.Medium,
                repositoryState.Evidence));
        }

        return benefits.ToArray();
    }

    private static DecisionCost[] CostsFor(
        DecisionCandidate candidate,
        DecisionOption option,
        IReadOnlyList<DecisionEvidence> optionEvidence,
        DecisionGenerationContext generationContext)
    {
        var costs = new List<DecisionCost>
        {
            new(
                CostFor(candidate, option),
                CostImpact(option),
                optionEvidence)
        };

        DecisionGenerationContextEntry? constraint = BestMatch(generationContext.Constraints, candidate, option);
        if (constraint is not null)
        {
            costs.Add(new DecisionCost(
                $"Must account for active constraint: {constraint.Statement}",
                TradeoffImpact.Medium,
                constraint.Evidence));
        }

        return costs.ToArray();
    }

    private static DecisionRisk[] RisksFor(
        DecisionCandidate candidate,
        DecisionOption option,
        IReadOnlyList<DecisionEvidence> evidence,
        DecisionGenerationContext generationContext)
    {
        var risks = new List<DecisionRisk>
        {
            new(
                RiskFor(candidate, option),
                RiskSeverity(candidate, option),
                false,
                evidence)
        };

        DecisionGenerationContextEntry? conflictingConstraint = ConstraintConflictFor(generationContext, option, candidate);
        if (conflictingConstraint is not null)
        {
            risks.Add(new DecisionRisk(
                $"Constraint may be violated by {option.Title}: {conflictingConstraint.Statement}",
                TradeoffSeverity.High,
                false,
                conflictingConstraint.Evidence));
        }

        DecisionGenerationContextEntry? contextualRisk = BestMatch(generationContext.Risks, candidate, option);
        if (contextualRisk is not null)
        {
            risks.Add(new DecisionRisk(
                $"Context risk remains relevant for {option.Title}: {contextualRisk.Statement}",
                TradeoffSeverity.Medium,
                false,
                contextualRisk.Evidence));
        }

        DecisionGenerationContextEntry? question = BestMatch(generationContext.Questions, candidate, option);
        if (question is not null)
        {
            risks.Add(new DecisionRisk(
                $"Unknown answer may affect {option.Title}: {question.Statement}",
                TradeoffSeverity.Medium,
                true,
                question.Evidence));
        }

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
        IReadOnlyList<DecisionEvidence> evidence,
        DecisionGenerationContext generationContext)
    {
        string[] optionDependencies = option.Dependencies.Count == 0
            ? ["Repository evidence remains available for human review."]
            : option.Dependencies.ToArray();

        return optionDependencies
            .Order(StringComparer.Ordinal)
            .Select(dependency => new DecisionDependency(dependency, evidence))
            .Concat(generationContext.Dependencies
                .Take(3)
                .Select(dependency => new DecisionDependency(
                    $"Generation context dependency: {dependency.Statement}",
                    dependency.Evidence)))
            .DistinctBy(dependency => dependency.Statement, StringComparer.OrdinalIgnoreCase)
            .OrderBy(dependency => dependency.Statement, StringComparer.Ordinal)
            .ToArray();
    }

    private static DecisionConsequence[] ConsequencesFor(
        DecisionCandidate candidate,
        DecisionOption option,
        IReadOnlyList<DecisionEvidence> optionEvidence,
        DecisionGenerationContext generationContext)
    {
        var consequences = new List<DecisionConsequence>
        {
            new(
                ConsequenceFor(candidate, option),
                ConsequenceImpact(option),
                optionEvidence)
        };

        DecisionGenerationContextEntry? priorDecision = BestMatch(generationContext.PriorDecisions, candidate, option);
        if (priorDecision is not null)
        {
            consequences.Add(new DecisionConsequence(
                $"May affect prior decision context: {priorDecision.Statement}",
                TradeoffImpact.Medium,
                priorDecision.Evidence));
        }

        DecisionGenerationContextEntry? handoff = BestMatch(generationContext.HandoffState, candidate, option);
        if (handoff is not null)
        {
            consequences.Add(new DecisionConsequence(
                $"Should preserve current handoff continuity: {handoff.Statement}",
                TradeoffImpact.Medium,
                handoff.Evidence));
        }

        return consequences.ToArray();
    }

    private static string[] DiagnosticsFor(
        DecisionCandidate candidate,
        DecisionOption option,
        DecisionGenerationContext generationContext,
        string contextFingerprint)
    {
        return [
            $"Analyzed {option.Id} as {option.Type} for {candidate.Classification} candidate {candidate.Id}.",
            $"Context fingerprint: {contextFingerprint}.",
            $"Generation context fingerprint: {generationContext.Fingerprint}.",
            $"Generation context inputs: goals={generationContext.Goals.Count}, constraints={generationContext.Constraints.Count}, risks={generationContext.Risks.Count}, questions={generationContext.Questions.Count}, priorDecisions={generationContext.PriorDecisions.Count}, repositoryState={generationContext.RepositoryState.Count}, dependencies={generationContext.Dependencies.Count}, handoffState={generationContext.HandoffState.Count}."
        ];
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

    private static DecisionGenerationContextEntry? BestMatch(
        IReadOnlyList<DecisionGenerationContextEntry> entries,
        DecisionCandidate candidate,
        DecisionOption option)
    {
        return entries
            .OrderByDescending(entry => MatchScore(entry.Statement, candidate, option))
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .FirstOrDefault(entry => MatchScore(entry.Statement, candidate, option) > 0) ??
            entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private static DecisionGenerationContextEntry? ConstraintConflictFor(
        DecisionGenerationContext generationContext,
        DecisionOption option,
        DecisionCandidate candidate)
    {
        return generationContext.Constraints
            .OrderByDescending(entry => MatchScore(entry.Statement, candidate, option))
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .FirstOrDefault(entry => OptionViolatesConstraint(option, entry.Statement));
    }

    private static bool OptionViolatesConstraint(DecisionOption option, string constraint)
    {
        if (option.Type == DecisionOptionType.Replace &&
            ContainsAny(constraint, "must not replace", "avoid replacement", "preserve", "compatibility"))
        {
            return true;
        }

        if (option.Type == DecisionOptionType.Delay &&
            ContainsAny(constraint, "must decide", "must resolve", "do not defer", "no deferral"))
        {
            return true;
        }

        if (option.Type == DecisionOptionType.Expand &&
            ContainsAny(constraint, "constrain", "narrow", "minimal", "bounded"))
        {
            return true;
        }

        return false;
    }

    private static int MatchScore(string statement, DecisionCandidate candidate, DecisionOption option)
    {
        string[] terms = candidate.Title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(option.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(term => term.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return terms.Count(term => statement.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsConstraintSignal(string kind)
    {
        return kind.Contains("Constraint", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Conflict", StringComparison.OrdinalIgnoreCase) ||
            kind.Contains("Contradiction", StringComparison.OrdinalIgnoreCase);
    }
}

using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class RecommendationService : IRecommendationService
{
    public DecisionRecommendation GenerateRecommendation(
        DecisionCandidate candidate,
        DecisionGenerationContext generationContext,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<AnalyzedDecisionOption> analyzedOptions,
        IReadOnlyList<DecisionTradeoffComparison> tradeoffComparisons,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        OptionEvaluation[] evaluations = BuildEvaluations(generationContext, options, analyzedOptions, tradeoffComparisons)
            .OrderByDescending(evaluation => evaluation.Score)
            .ThenBy(evaluation => evaluation.OptionId, StringComparer.Ordinal)
            .Select((evaluation, index) => evaluation with { Rank = index + 1 })
            .ToArray();
        OptionEvaluation[] viable = evaluations
            .Where(evaluation => evaluation.Constraints.Count == 0)
            .ToArray();
        DecisionEvidence[] recommendationEvidence = EvidenceForRecommendation(candidate, evidence);
        RecommendationEvidence[] contextEvidence = ContextEvidenceForRecommendation(generationContext);

        if (viable.Length == 0)
        {
            return NoRecommendation(
                "No option can be recommended because every generated option carries a disqualifying constraint.",
                candidate,
                generationContext,
                evaluations,
                recommendationEvidence,
                contextEvidence);
        }

        if (HasInsufficientEvidence(evidence))
        {
            return NoRecommendation(
                "No option can be recommended because candidate evidence is insufficient to support decision authority.",
                candidate,
                generationContext,
                evaluations,
                recommendationEvidence,
                contextEvidence);
        }

        if (HasUnresolvedContradiction(candidate))
        {
            return NoRecommendation(
                "No option can be recommended because the candidate contains an unresolved contradiction that requires human review.",
                candidate,
                generationContext,
                evaluations,
                recommendationEvidence,
                contextEvidence);
        }

        OptionEvaluation best = viable[0];
        OptionEvaluation? runnerUp = viable.Skip(1).FirstOrDefault();
        if (HasExcessiveUncertainty(viable))
        {
            return NoRecommendation(
                "No option can be recommended because uncertainty or unresolved tradeoff parity prevents a defensible preference.",
                candidate,
                generationContext,
                evaluations,
                recommendationEvidence,
                contextEvidence);
        }

        string[] supportingFactors = best.Strengths
            .Concat(best.Evidence
                .Where(item => item.Type is RecommendationEvidenceType.Benefit or RecommendationEvidenceType.Consequence)
                .Select(item => item.Summary))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        string[] concerns = best.Weaknesses
            .Concat(best.Risks)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        string[] alternatives = viable
            .Where(evaluation => evaluation.OptionId != best.OptionId)
            .OrderBy(evaluation => evaluation.Rank)
            .Select(evaluation => $"{evaluation.OptionId} lost because {LosingRationale(evaluation, best)}")
            .ToArray();
        RecommendationMode mode = runnerUp is null
            ? RecommendationMode.PreferredOption
            : RecommendationMode.PreferredPlusAlternative;
        string alternativeClause = alternatives.Length == 0
            ? "No generated alternative scored close enough to present as a preferred fallback."
            : alternatives[0];

        return new DecisionRecommendation(
            best.OptionId,
            $"Advisory recommendation: prefer {best.OptionId} for '{candidate.Title}' because {best.Summary} {alternativeClause}",
            recommendationEvidence)
        {
            Summary = $"Prefer {best.OptionId}: {best.Summary}",
            SupportingFactors = supportingFactors,
            Concerns = ConcernsFor(concerns, generationContext),
            Assumptions = AssumptionsFor(candidate, generationContext),
            AlternativeExplanations = alternatives,
            Mode = mode,
            RecommendationEvidence = best.Evidence
                .Concat(contextEvidence.Select(item => item with { OptionId = best.OptionId }))
                .ToArray(),
            OptionEvaluations = evaluations
        };
    }

    private static OptionEvaluation[] BuildEvaluations(
        DecisionGenerationContext generationContext,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<AnalyzedDecisionOption> analyzedOptions,
        IReadOnlyList<DecisionTradeoffComparison> tradeoffComparisons)
    {
        var evaluations = new List<OptionEvaluation>();
        foreach (DecisionOption option in options.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            AnalyzedDecisionOption? analyzed = analyzedOptions
                .FirstOrDefault(item => string.Equals(item.OptionId, option.Id, StringComparison.Ordinal));
            DecisionTradeoffComparison? comparison = tradeoffComparisons
                .FirstOrDefault(item => string.Equals(item.OptionId, option.Id, StringComparison.Ordinal));
            DecisionBenefit[] benefits = analyzed?.Benefits.ToArray() ?? [];
            DecisionCost[] costs = analyzed?.Costs.ToArray() ?? [];
            DecisionRisk[] risks = analyzed?.Risks.ToArray() ?? [];
            DecisionDependency[] dependencies = analyzed?.Dependencies.ToArray() ?? [];
            DecisionConsequence[] consequences = analyzed?.Consequences.ToArray() ?? [];
            string[] strengths = comparison?.RelativeStrengths
                .Concat(comparison.UniqueAdvantages)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [];
            string[] weaknesses = comparison?.RelativeWeaknesses
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [];
            string[] constraints = comparison?.DisqualifyingConstraints
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [];
            string[] riskStatements = comparison?.UniqueRisks
                .Concat(risks.Select(risk => risk.Statement))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? risks.Select(risk => risk.Statement).ToArray();
            int priorityAdjustment = PriorityAdjustmentFor(generationContext, option);
            string[] adjustedStrengths = priorityAdjustment > 0
                ? strengths.Concat([$"Priority directive favors timely progress for {option.Id}."])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : strengths;
            string[] adjustedWeaknesses = priorityAdjustment < 0
                ? weaknesses.Concat([$"Priority directive lowers fit for {option.Id}."])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : weaknesses;
            int score = Score(benefits, costs, risks, dependencies, consequences, adjustedStrengths, constraints) + priorityAdjustment;

            evaluations.Add(new OptionEvaluation(
                option.Id,
                adjustedStrengths,
                adjustedWeaknesses,
                riskStatements,
                constraints,
                SummaryFor(option, benefits, costs, risks, consequences, constraints, score),
                score,
                0,
                ScoreExplanationFor(benefits, costs, risks, dependencies, consequences, adjustedStrengths, constraints, score, priorityAdjustment),
                EvidenceFor(option.Id, benefits, costs, risks, dependencies, consequences, constraints, comparison)));
        }

        return evaluations.ToArray();
    }

    private static int Score(
        IReadOnlyList<DecisionBenefit> benefits,
        IReadOnlyList<DecisionCost> costs,
        IReadOnlyList<DecisionRisk> risks,
        IReadOnlyList<DecisionDependency> dependencies,
        IReadOnlyList<DecisionConsequence> consequences,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> constraints)
    {
        int score = benefits.Sum(benefit => ImpactPoints(benefit.Impact));
        score += consequences.Sum(consequence => ImpactPoints(consequence.Impact));
        score += strengths.Count;
        score -= costs.Sum(cost => ImpactPoints(cost.Impact));
        score -= risks.Sum(risk => SeverityPoints(risk.Severity) + (risk.IsUnknown ? 2 : 0));
        score -= dependencies.Count;
        score -= constraints.Count * 100;
        return score;
    }

    private static RecommendationEvidence[] EvidenceFor(
        string optionId,
        IReadOnlyList<DecisionBenefit> benefits,
        IReadOnlyList<DecisionCost> costs,
        IReadOnlyList<DecisionRisk> risks,
        IReadOnlyList<DecisionDependency> dependencies,
        IReadOnlyList<DecisionConsequence> consequences,
        IReadOnlyList<string> constraints,
        DecisionTradeoffComparison? comparison)
    {
        return benefits.Select(benefit => new RecommendationEvidence(
                RecommendationEvidenceType.Benefit,
                optionId,
                benefit.Statement,
                benefit.Evidence))
            .Concat(costs.Select(cost => new RecommendationEvidence(
                RecommendationEvidenceType.Cost,
                optionId,
                cost.Statement,
                cost.Evidence)))
            .Concat(risks.Select(risk => new RecommendationEvidence(
                RecommendationEvidenceType.Risk,
                optionId,
                risk.Statement,
                risk.Evidence)))
            .Concat(dependencies.Select(dependency => new RecommendationEvidence(
                RecommendationEvidenceType.Dependency,
                optionId,
                dependency.Statement,
                dependency.Evidence)))
            .Concat(consequences.Select(consequence => new RecommendationEvidence(
                RecommendationEvidenceType.Consequence,
                optionId,
                consequence.Statement,
                consequence.Evidence)))
            .Concat(constraints.Select(constraint => new RecommendationEvidence(
                RecommendationEvidenceType.Constraint,
                optionId,
                constraint,
                comparison?.Evidence ?? [])))
            .ToArray();
    }

    private static DecisionRecommendation NoRecommendation(
        string reason,
        DecisionCandidate candidate,
        DecisionGenerationContext generationContext,
        IReadOnlyList<OptionEvaluation> evaluations,
        IReadOnlyList<DecisionEvidence> evidence,
        IReadOnlyList<RecommendationEvidence> contextEvidence)
    {
        return new DecisionRecommendation(
            string.Empty,
            $"Advisory recommendation withheld for '{candidate.Title}'. {reason}",
            evidence)
        {
            Summary = "No defensible recommendation.",
            SupportingFactors = [],
            Concerns = ConcernsFor([reason], generationContext),
            Assumptions = AssumptionsFor(candidate, generationContext),
            AlternativeExplanations = evaluations
                .OrderBy(evaluation => evaluation.Rank)
                .Select(evaluation => $"{evaluation.OptionId} was not recommended because {evaluation.Summary}")
                .ToArray(),
            Mode = RecommendationMode.NoRecommendation,
            RecommendationEvidence = evaluations
                .SelectMany(evaluation => evaluation.Evidence)
                .Concat(contextEvidence)
                .ToArray(),
            OptionEvaluations = evaluations
        };
    }

    private static bool HasInsufficientEvidence(IReadOnlyList<DecisionEvidence> evidence)
    {
        return !evidence.Any(item => item.Sources.Any(source =>
            source.RelativePath is not null &&
            !source.RelativePath.Contains(".agents/decisions/candidates/", StringComparison.OrdinalIgnoreCase) &&
            !source.RelativePath.Contains(".agents/decisions/proposals/", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasUnresolvedContradiction(DecisionCandidate candidate)
    {
        return candidate.Signals.Any(signal =>
            signal.Kind.Contains("Contradiction", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExcessiveUncertainty(IReadOnlyList<OptionEvaluation> viable)
    {
        return viable.All(evaluation =>
            evaluation.Evidence.Any(item =>
                item.Type == RecommendationEvidenceType.Risk &&
                item.Summary.Contains("Unknown", StringComparison.OrdinalIgnoreCase)));
    }

    private static string SummaryFor(
        DecisionOption option,
        IReadOnlyList<DecisionBenefit> benefits,
        IReadOnlyList<DecisionCost> costs,
        IReadOnlyList<DecisionRisk> risks,
        IReadOnlyList<DecisionConsequence> consequences,
        IReadOnlyList<string> constraints,
        int score)
    {
        if (constraints.Count > 0)
        {
            return $"{option.Title} is blocked by {constraints.Count} disqualifying constraint(s).";
        }

        DecisionBenefit? benefit = benefits
            .OrderByDescending(item => item.Impact)
            .ThenBy(item => item.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        DecisionRisk? risk = risks
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.IsUnknown)
            .ThenBy(item => item.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        DecisionConsequence? consequence = consequences
            .OrderByDescending(item => item.Impact)
            .ThenBy(item => item.Statement, StringComparer.Ordinal)
            .FirstOrDefault();
        string benefitText = benefit?.Statement ?? "no generated benefit dominates";
        string riskText = risk?.Statement ?? "no generated risk dominates";
        string consequenceText = consequence?.Statement ?? "no generated consequence dominates";
        return $"{benefitText}; main risk: {riskText}; consequence: {consequenceText}; explainable score {score}.";
    }

    private static string ScoreExplanationFor(
        IReadOnlyList<DecisionBenefit> benefits,
        IReadOnlyList<DecisionCost> costs,
        IReadOnlyList<DecisionRisk> risks,
        IReadOnlyList<DecisionDependency> dependencies,
        IReadOnlyList<DecisionConsequence> consequences,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> constraints,
        int score,
        int priorityAdjustment)
    {
        return $"Score {score} = benefits {benefits.Sum(benefit => ImpactPoints(benefit.Impact))} + consequences {consequences.Sum(consequence => ImpactPoints(consequence.Impact))} + comparison strengths {strengths.Count} + priority adjustment {priorityAdjustment} - costs {costs.Sum(cost => ImpactPoints(cost.Impact))} - risks {risks.Sum(risk => SeverityPoints(risk.Severity) + (risk.IsUnknown ? 2 : 0))} - dependencies {dependencies.Count} - disqualifying constraints {constraints.Count * 100}.";
    }

    private static int PriorityAdjustmentFor(DecisionGenerationContext generationContext, DecisionOption option)
    {
        bool increasePriority = HasPriorityDirective(generationContext, "IncreasePriority");
        bool decreasePriority = HasPriorityDirective(generationContext, "DecreasePriority");
        if (!increasePriority && !decreasePriority)
        {
            return 0;
        }

        bool defersDecision = option.Type is DecisionOptionType.Delay or DecisionOptionType.Investigate;
        if (increasePriority)
        {
            return defersDecision ? -2 : 2;
        }

        return defersDecision ? 2 : -1;
    }

    private static bool HasPriorityDirective(DecisionGenerationContext generationContext, string directiveType)
    {
        return generationContext.Goals.Any(goal =>
                goal.Statement.Contains(directiveType, StringComparison.OrdinalIgnoreCase)) ||
            generationContext.Diagnostics.Any(diagnostic =>
                diagnostic.Contains(directiveType, StringComparison.OrdinalIgnoreCase));
    }

    private static string LosingRationale(OptionEvaluation alternative, OptionEvaluation winner)
    {
        if (alternative.Constraints.Count > 0)
        {
            return "it has disqualifying constraints.";
        }

        if (alternative.Score < winner.Score)
        {
            return $"its explainable score {alternative.Score} is below {winner.OptionId}'s score {winner.Score}.";
        }

        return "its strengths did not clearly exceed its generated risks and costs.";
    }

    private static string[] ConcernsFor(IReadOnlyList<string> concerns, DecisionGenerationContext generationContext)
    {
        return concerns
            .Concat(generationContext.Risks.Take(3).Select(risk => $"Context risk: {risk.Statement}"))
            .Concat(generationContext.Questions.Take(3).Select(question => $"Open question: {question.Statement}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .DefaultIfEmpty("Human review is still required before this recommendation can become decision authority.")
            .ToArray();
    }

    private static string[] AssumptionsFor(DecisionCandidate candidate, DecisionGenerationContext generationContext)
    {
        return [
            $"Candidate {candidate.Id} remains promoted and current.",
            $"Generation context fingerprint {generationContext.Fingerprint} reflects the repository evidence used for this recommendation.",
            "Recommendation output is advisory and does not resolve the decision."
        ];
    }

    private static DecisionEvidence[] EvidenceForRecommendation(
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

    private static RecommendationEvidence[] ContextEvidenceForRecommendation(DecisionGenerationContext generationContext)
    {
        return generationContext.PriorDecisions
            .Take(3)
            .Select(entry => new RecommendationEvidence(
                RecommendationEvidenceType.PriorDecision,
                string.Empty,
                entry.Statement,
                entry.Evidence))
            .Concat(generationContext.RepositoryState
                .Take(3)
                .Select(entry => new RecommendationEvidence(
                    RecommendationEvidenceType.RepositoryState,
                    string.Empty,
                    entry.Statement,
                    entry.Evidence)))
            .ToArray();
    }

    private static int ImpactPoints(TradeoffImpact impact)
    {
        return impact switch
        {
            TradeoffImpact.Low => 1,
            TradeoffImpact.Medium => 2,
            TradeoffImpact.High => 4,
            TradeoffImpact.Blocking => 6,
            _ => 0
        };
    }

    private static int SeverityPoints(TradeoffSeverity severity)
    {
        return severity switch
        {
            TradeoffSeverity.Info => 0,
            TradeoffSeverity.Low => 1,
            TradeoffSeverity.Medium => 3,
            TradeoffSeverity.High => 5,
            TradeoffSeverity.Blocking => 7,
            _ => 0
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionPackageService(
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService,
    ITradeoffAnalysisService? tradeoffAnalysisService = null,
    IOptionComparisonService? optionComparisonService = null,
    IRecommendationService? recommendationService = null) : IDecisionPackageService
{
    private const string GeneratorVersion = "deterministic-package-v1";
    private const string RefinementGeneratorVersion = "deterministic-refinement-v1";
    private const string MilestoneId = "M6";
    private const string MilestonePath = ".agents/milestones/m6-decision-packages.md";
    private const string RefinementMilestoneId = "M7";
    private const string RefinementMilestonePath = ".agents/milestones/m7-decision-refinement.md";
    private readonly ITradeoffAnalysisService tradeoffAnalysisService = tradeoffAnalysisService ?? new TradeoffAnalysisService();
    private readonly IOptionComparisonService optionComparisonService = optionComparisonService ?? new OptionComparisonService();
    private readonly IRecommendationService recommendationService = recommendationService ?? new RecommendationService();

    public async Task<DecisionPackageVersion> CreatePackageAsync(
        Repository repository,
        DecisionCandidate candidate,
        DecisionProposal proposal,
        DecisionGenerationContext generationContext,
        DateTimeOffset generatedAt)
    {
        if (candidate.RepositoryId != repository.Id || proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision package inputs must belong to the target repository.");
        }

        if (!string.Equals(proposal.CandidateId, candidate.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Decision package proposal must reference the supplied candidate.");
        }

        string packageId = await decisionRepository.AllocatePackageVersionIdAsync(repository, proposal.Id);
        string sourceProposalFingerprint = Fingerprint(proposal);
        string contextFingerprint = string.IsNullOrWhiteSpace(generationContext.Fingerprint)
            ? Fingerprint(generationContext)
            : generationContext.Fingerprint;
        var metadata = new DecisionPackageMetadata(
            contextFingerprint,
            GeneratorVersion,
            candidate.Id,
            Fingerprint(new
            {
                generationContext.RepositoryState,
                generationContext.Dependencies,
                generationContext.HandoffState,
                candidate.SourceFingerprint
            }),
            MilestoneId,
            MilestonePath,
            proposal.Id,
            sourceProposalFingerprint);
        var package = new DecisionPackage(
            packageId,
            repository.Id,
            proposal.Id,
            candidate.Id,
            proposal.Title,
            proposal.Context,
            candidate,
            generationContext,
            proposal.Options,
            proposal.OptionRelationships,
            proposal.AnalyzedOptions,
            proposal.Tradeoffs,
            proposal.TradeoffComparisons,
            proposal.Recommendation,
            proposal.Assumptions,
            proposal.Recommendation?.Concerns ?? [],
            proposal.Evidence,
            metadata,
            proposal.GenerationDiagnostics,
            proposal.TradeoffAnalysisDiagnostics,
            generatedAt);
        var packageVersion = new DecisionPackageVersion(
            packageId,
            repository.Id,
            proposal.Id,
            candidate.Id,
            generatedAt,
            Fingerprint(package),
            package);

        DecisionPackageValidationResult validation = ValidatePackage(package);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Decision package {package.Id} failed validation: {string.Join("; ", validation.Errors)}");
        }

        await decisionRepository.SavePackageVersionAsync(repository, packageVersion);
        await projectionService.ProjectPackageVersionAsync(repository, packageVersion);
        return packageVersion;
    }

    public async Task<DecisionPackageRegenerationResult> RegeneratePackageAsync(
        Repository repository,
        DecisionProposal proposal,
        DecisionPackageVersion basePackageVersion,
        DecisionPackageRegenerationRequest request,
        DateTimeOffset generatedAt)
    {
        if (request is null)
        {
            throw new ArgumentException("Package regeneration request is required.", nameof(request));
        }

        if (request.Plan is null)
        {
            throw new ArgumentException("Refinement plan is required.", nameof(request));
        }

        RefinementPlan plan = request.Plan;
        if (proposal.RepositoryId != repository.Id ||
            basePackageVersion.RepositoryId != repository.Id ||
            basePackageVersion.Package.RepositoryId != repository.Id ||
            plan.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Package regeneration inputs must belong to the target repository.");
        }

        if (!string.Equals(proposal.Id, plan.ProposalId, StringComparison.Ordinal) ||
            !string.Equals(proposal.Id, basePackageVersion.ProposalId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Package regeneration inputs must target the same proposal.");
        }

        if (!string.Equals(request.BasePackageId, basePackageVersion.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Package regeneration base package id is stale.");
        }

        if (!string.Equals(request.BasePackageFingerprint, basePackageVersion.PackageFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Package regeneration base package fingerprint is stale.");
        }

        string currentProposalFingerprint = Fingerprint(proposal);
        if (!string.Equals(plan.BaseProposalFingerprint, currentProposalFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refinement plan base proposal fingerprint is stale.");
        }

        if (!plan.RegenerateOptions &&
            !plan.ReevaluateTradeoffs &&
            !plan.ReevaluateRecommendation &&
            !plan.FullRegeneration)
        {
            throw new InvalidOperationException("Refinement plan does not request package regeneration.");
        }

        DecisionPackage basePackage = basePackageVersion.Package;
        DecisionCandidate candidate = basePackage.Candidate;
        DecisionGenerationContext generationContext = ApplyPlanToContext(basePackage.ContextSummary, plan, generatedAt);
        DecisionOption[] options = plan.RegenerateOptions || plan.FullRegeneration
            ? RegenerateOptions(basePackage, plan, generatedAt)
            : basePackage.Options.ToArray();
        DecisionOptionRelationship[] relationships = plan.RegenerateOptions || plan.FullRegeneration
            ? RegenerateRelationships(basePackage, options)
            : basePackage.OptionRelationships.ToArray();
        AnalyzedDecisionOption[] analyzedOptions = plan.ReevaluateTradeoffs || plan.RegenerateOptions || plan.FullRegeneration
            ? this.tradeoffAnalysisService.AnalyzeOptions(
                    candidate,
                    options,
                    basePackage.Evidence,
                    generationContext,
                    generationContext.Fingerprint)
                .ToArray()
            : basePackage.AnalyzedOptions.ToArray();
        DecisionTradeoffComparison[] tradeoffComparisons = plan.ReevaluateTradeoffs || plan.RegenerateOptions || plan.FullRegeneration
            ? this.optionComparisonService.CompareOptions(candidate, analyzedOptions, relationships, basePackage.Evidence).ToArray()
            : basePackage.TradeoffComparisons.ToArray();
        DecisionTradeoff[] tradeoffs = plan.ReevaluateTradeoffs || plan.RegenerateOptions || plan.FullRegeneration
            ? BuildTradeoffs(analyzedOptions)
            : basePackage.Tradeoffs.ToArray();
        DecisionRecommendation? recommendation = plan.ReevaluateRecommendation ||
            plan.ReevaluateTradeoffs ||
            plan.RegenerateOptions ||
            plan.FullRegeneration
                ? this.recommendationService.GenerateRecommendation(
                    candidate,
                    generationContext,
                    options,
                    analyzedOptions,
                    tradeoffComparisons,
                    basePackage.Evidence)
                : basePackage.Recommendation;
        string packageId = await decisionRepository.AllocatePackageVersionIdAsync(repository, proposal.Id);
        DecisionGenerationDiagnostics generationDiagnostics = BuildGenerationDiagnostics(
            basePackage.GenerationDiagnostics,
            plan,
            options.Length,
            basePackage.Options.Count);
        DecisionTradeoffAnalysisDiagnostics? tradeoffDiagnostics = BuildTradeoffDiagnostics(
            basePackage.TradeoffAnalysisDiagnostics,
            plan,
            analyzedOptions.Length,
            generationContext.Fingerprint);
        DecisionPackageMetadata metadata = basePackage.Metadata with
        {
            ContextFingerprint = generationContext.Fingerprint,
            GeneratorVersion = RefinementGeneratorVersion,
            RepositoryStateFingerprint = Fingerprint(new
            {
                generationContext.RepositoryState,
                generationContext.Dependencies,
                generationContext.HandoffState,
                plan.Directives,
                basePackageVersion.PackageFingerprint
            }),
            MilestoneId = RefinementMilestoneId,
            MilestonePath = RefinementMilestonePath,
            SourceProposalId = proposal.Id,
            SourceProposalFingerprint = currentProposalFingerprint
        };
        DecisionPackage package = basePackage with
        {
            Id = packageId,
            ContextSummary = generationContext,
            Options = options,
            OptionRelationships = relationships,
            AnalyzedOptions = analyzedOptions,
            Tradeoffs = tradeoffs,
            TradeoffComparisons = tradeoffComparisons,
            Recommendation = recommendation,
            OpenConcerns = recommendation?.Concerns ?? basePackage.OpenConcerns,
            Metadata = metadata,
            GenerationDiagnostics = generationDiagnostics,
            TradeoffAnalysisDiagnostics = tradeoffDiagnostics,
            GeneratedAt = generatedAt
        };
        var packageVersion = new DecisionPackageVersion(
            packageId,
            repository.Id,
            proposal.Id,
            candidate.Id,
            generatedAt,
            Fingerprint(package),
            package);

        DecisionPackageValidationResult validation = ValidatePackage(package);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Decision package {package.Id} failed validation: {string.Join("; ", validation.Errors)}");
        }

        DecisionPackageComparison comparison = ComparePackages(basePackageVersion, packageVersion);
        await decisionRepository.SavePackageVersionAsync(repository, packageVersion);
        await projectionService.ProjectPackageVersionAsync(repository, packageVersion);
        await projectionService.ProjectPackageComparisonAsync(repository, comparison);

        string[] diagnostics = [
            $"Regenerated package {packageVersion.Id} from {basePackageVersion.Id} using analyzed refinement plan.",
            $"Regeneration scopes: options={plan.RegenerateOptions}, tradeoffs={plan.ReevaluateTradeoffs}, recommendation={plan.ReevaluateRecommendation}, full={plan.FullRegeneration}.",
            $"Base package fingerprint {basePackageVersion.PackageFingerprint} was preserved; prior package versions remain immutable.",
            "Human authoring burden classified as MajorRefinement because reviewer guidance produced a scoped regenerated package version.",
            .. plan.Diagnostics
        ];
        return new DecisionPackageRegenerationResult(
            repository.Id,
            proposal.Id,
            plan,
            basePackageVersion,
            packageVersion,
            comparison,
            HumanAuthoringBurden.MajorRefinement,
            diagnostics);
    }

    public DecisionPackageValidationResult ValidatePackage(DecisionPackage package)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(package.DecisionSummary))
        {
            errors.Add("Decision summary is required.");
        }

        if (!HasContext(package.ContextSummary))
        {
            errors.Add("Decision context is required.");
        }

        if (package.Options.Count == 0)
        {
            errors.Add("At least one generated option is required.");
        }

        if (package.Options.Count == 1 && !HasSingleOptionJustification(package))
        {
            errors.Add("At least two generated options are required unless the package explicitly justifies a single technically valid path.");
        }

        if (package.Evidence.Count == 0)
        {
            errors.Add("Package evidence is required.");
        }

        ValidateRecommendation(package, errors, warnings);

        return new DecisionPackageValidationResult(errors.Count == 0, errors, warnings);
    }

    public DecisionPackageComparison ComparePackages(DecisionPackageVersion left, DecisionPackageVersion right)
    {
        if (left.RepositoryId != right.RepositoryId)
        {
            throw new InvalidOperationException("Decision packages must belong to the same repository.");
        }

        if (!string.Equals(left.ProposalId, right.ProposalId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Decision packages must belong to the same proposal.");
        }

        DecisionPackage leftPackage = left.Package;
        DecisionPackage rightPackage = right.Package;
        bool recommendationChanged = Fingerprint(leftPackage.Recommendation) != Fingerprint(rightPackage.Recommendation);
        bool contextFingerprintChanged = !string.Equals(
            leftPackage.Metadata.ContextFingerprint,
            rightPackage.Metadata.ContextFingerprint,
            StringComparison.Ordinal);

        IReadOnlyList<DecisionOption> addedOptions = rightPackage.Options
            .Where(rightOption => leftPackage.Options.All(leftOption => !SameId(leftOption.Id, rightOption.Id)))
            .OrderBy(option => option.Id, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<DecisionOption> removedOptions = leftPackage.Options
            .Where(leftOption => rightPackage.Options.All(rightOption => !SameId(rightOption.Id, leftOption.Id)))
            .OrderBy(option => option.Id, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<DecisionOption> modifiedOptions = rightPackage.Options
            .Where(rightOption => leftPackage.Options.Any(leftOption =>
                SameId(leftOption.Id, rightOption.Id) &&
                Fingerprint(leftOption) != Fingerprint(rightOption)))
            .OrderBy(option => option.Id, StringComparer.Ordinal)
            .ToArray();
        bool optionsChanged = addedOptions.Count > 0 || removedOptions.Count > 0 || modifiedOptions.Count > 0;

        string[] leftEvidence = EvidenceKeys(leftPackage).ToArray();
        string[] rightEvidence = EvidenceKeys(rightPackage).ToArray();
        IReadOnlyList<string> addedEvidence = rightEvidence.Except(leftEvidence, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> removedEvidence = leftEvidence.Except(rightEvidence, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool evidenceChanged = addedEvidence.Count > 0 || removedEvidence.Count > 0;

        string[] leftRisks = RiskKeys(leftPackage).ToArray();
        string[] rightRisks = RiskKeys(rightPackage).ToArray();
        IReadOnlyList<string> addedRisks = rightRisks.Except(leftRisks, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> removedRisks = leftRisks.Except(rightRisks, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool risksChanged = addedRisks.Count > 0 || removedRisks.Count > 0;

        DecisionRevisionFieldComparison[] fieldComparisons = [
            new(
                "Recommendation",
                recommendationChanged ? "Changed" : "Unchanged",
                RecommendationSummary(leftPackage.Recommendation),
                RecommendationSummary(rightPackage.Recommendation)),
            new(
                "Options",
                optionsChanged ? "Changed" : "Unchanged",
                OptionSummary(leftPackage.Options),
                OptionSummary(rightPackage.Options)),
            new(
                "Evidence",
                evidenceChanged ? "Changed" : "Unchanged",
                leftEvidence.Length.ToString(),
                rightEvidence.Length.ToString()),
            new(
                "Risks",
                risksChanged ? "Changed" : "Unchanged",
                leftRisks.Length.ToString(),
                rightRisks.Length.ToString()),
            new(
                "ContextFingerprint",
                contextFingerprintChanged ? "Changed" : "Unchanged",
                leftPackage.Metadata.ContextFingerprint,
                rightPackage.Metadata.ContextFingerprint)
        ];

        var diagnostics = new List<string>();
        if (!recommendationChanged && !optionsChanged && !evidenceChanged && !risksChanged && !contextFingerprintChanged)
        {
            diagnostics.Add("No package comparison changes detected.");
        }

        return new DecisionPackageComparison(
            left.ProposalId,
            left.Id,
            right.Id,
            left.RepositoryId,
            left.PackageFingerprint,
            right.PackageFingerprint,
            recommendationChanged,
            optionsChanged,
            evidenceChanged,
            risksChanged,
            contextFingerprintChanged,
            fieldComparisons,
            addedOptions,
            removedOptions,
            modifiedOptions,
            addedEvidence,
            removedEvidence,
            addedRisks,
            removedRisks,
            diagnostics);
    }

    private static void ValidateRecommendation(
        DecisionPackage package,
        List<string> errors,
        List<string> warnings)
    {
        if (package.Recommendation is null)
        {
            errors.Add("A recommendation or no-recommendation explanation is required.");
            return;
        }

        DecisionRecommendation recommendation = package.Recommendation;
        if (recommendation.Mode == RecommendationMode.NoRecommendation)
        {
            if (string.IsNullOrWhiteSpace(recommendation.Rationale) &&
                string.IsNullOrWhiteSpace(recommendation.Summary) &&
                recommendation.Concerns.Count == 0)
            {
                errors.Add("No-recommendation packages must explain why no option can be recommended.");
            }

            if (!string.IsNullOrWhiteSpace(recommendation.OptionId))
            {
                warnings.Add("No-recommendation packages should not bind to a recommended option id.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(recommendation.OptionId))
        {
            errors.Add("Recommended option id is required when the recommendation selects an option.");
        }
        else if (!package.Options.Any(option => string.Equals(option.Id, recommendation.OptionId, StringComparison.Ordinal)))
        {
            errors.Add($"Recommended option id '{recommendation.OptionId}' does not exist in the package options.");
        }

        if (recommendation.Evidence.Count == 0 && recommendation.RecommendationEvidence.Count == 0)
        {
            errors.Add("Recommendation evidence is required when a recommendation selects an option.");
        }
    }

    private static bool HasContext(DecisionGenerationContext context)
    {
        return !string.IsNullOrWhiteSpace(context.Fingerprint) ||
            context.Goals.Count > 0 ||
            context.Constraints.Count > 0 ||
            context.Risks.Count > 0 ||
            context.Questions.Count > 0 ||
            context.PriorDecisions.Count > 0 ||
            context.RepositoryState.Count > 0 ||
            context.Dependencies.Count > 0 ||
            context.HandoffState.Count > 0 ||
            context.Diagnostics.Count > 0;
    }

    private static bool HasSingleOptionJustification(DecisionPackage package)
    {
        string[] justifications = [
            package.Recommendation?.Rationale ?? string.Empty,
            .. package.OpenConcerns,
            .. package.GenerationDiagnostics?.Diagnostics ?? Array.Empty<string>()
        ];
        return justifications.Any(justification =>
            justification.Contains("only one", StringComparison.OrdinalIgnoreCase) ||
            justification.Contains("single technically valid", StringComparison.OrdinalIgnoreCase) ||
            justification.Contains("one technically valid", StringComparison.OrdinalIgnoreCase));
    }

    private static DecisionGenerationContext ApplyPlanToContext(
        DecisionGenerationContext context,
        RefinementPlan plan,
        DateTimeOffset generatedAt)
    {
        DecisionSourceReference source = new(
            "RefinementPlan",
            $".agents/decisions/proposals/{plan.ProposalId}/refinements/analyze",
            ProposalId: plan.ProposalId);
        DecisionGenerationContextEntry[] constraints = plan.AppliedConstraints
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint))
            .Select((constraint, index) => new DecisionGenerationContextEntry(
                $"refinement-constraint-{index + 1}",
                constraint.Trim(),
                [new DecisionEvidence($"Reviewer refinement constraint: {constraint.Trim()}", [source])]))
            .ToArray();
        string[] diagnostics = [
            .. context.Diagnostics,
            $"Refinement plan analyzed at {plan.AnalyzedAt:O} was applied during package regeneration at {generatedAt:O}.",
            $"Directive scopes: {string.Join(", ", plan.Directives.Select(directive => directive.Type).Distinct().Order())}."
        ];
        DecisionGenerationContext updated = context with
        {
            Constraints = context.Constraints
                .Concat(constraints)
                .DistinctBy(entry => entry.Statement, StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ToArray(),
            Diagnostics = diagnostics
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };

        return updated with { Fingerprint = Fingerprint(updated) };
    }

    private static DecisionOption[] RegenerateOptions(
        DecisionPackage basePackage,
        RefinementPlan plan,
        DateTimeOffset generatedAt)
    {
        DecisionOption[] existing = basePackage.Options.ToArray();
        string optionId = NextOptionId(existing);
        string directiveSummary = string.Join(
            ", ",
            plan.Directives
                .Select(directive => directive.Type.ToString())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        string constraintSummary = plan.AppliedConstraints.Count == 0
            ? "No additional reviewer constraints were extracted."
            : string.Join(" ", plan.AppliedConstraints.Order(StringComparer.Ordinal));
        DecisionSourceReference source = new(
            "RefinementPlan",
            $".agents/decisions/proposals/{plan.ProposalId}/refinements/analyze",
            ProposalId: plan.ProposalId);
        DecisionEvidence evidence = new(
            $"Refinement plan requested scoped package regeneration: {directiveSummary}.",
            [source]);
        var option = new DecisionOption(
            optionId,
            $"Apply reviewer-guided alternative for {basePackage.Candidate.Title}",
            $"Regenerate the package around reviewer directives ({directiveSummary}) while preserving package authority. {constraintSummary}",
            [evidence, .. basePackage.Evidence.Take(3)])
        {
            Type = plan.FullRegeneration ? DecisionOptionType.Refactor : DecisionOptionType.Constrain,
            Assumptions =
            [
                $"Regeneration uses analyzed directives from {plan.AnalyzedAt:O}.",
                "Prior package versions remain immutable and reviewable.",
                "Human resolution is still required before execution can consume the regenerated recommendation."
            ],
            Dependencies =
            [
                $"Base package {basePackage.Id} remains available for comparison.",
                $"Proposal fingerprint {plan.BaseProposalFingerprint} remains current."
            ],
            Diagnostics =
            [
                $"Created by deterministic scoped regeneration at {generatedAt:O}.",
                $"Applied directive types: {directiveSummary}."
            ]
        };

        return existing
            .Concat([option])
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static DecisionOptionRelationship[] RegenerateRelationships(
        DecisionPackage basePackage,
        IReadOnlyList<DecisionOption> options)
    {
        DecisionOption? regenerated = options
            .OrderByDescending(option => OptionNumber(option.Id))
            .FirstOrDefault(option => option.Diagnostics.Any(diagnostic =>
                diagnostic.Contains("deterministic scoped regeneration", StringComparison.OrdinalIgnoreCase)));
        DecisionOption? previousRecommendation = basePackage.Options.FirstOrDefault(option =>
            string.Equals(option.Id, basePackage.Recommendation?.OptionId, StringComparison.Ordinal));
        if (regenerated is null || previousRecommendation is null)
        {
            return basePackage.OptionRelationships.ToArray();
        }

        DecisionEvidence[] evidence = regenerated.Evidence.ToArray();
        var relationship = new DecisionOptionRelationship(
            regenerated.Id,
            previousRecommendation.Id,
            DecisionOptionRelationshipType.AlternativeTo,
            $"Regenerated option is a reviewer-guided alternative to the previously recommended option {previousRecommendation.Id}.",
            evidence);
        return basePackage.OptionRelationships
            .Concat([relationship])
            .DistinctBy(item => new { item.SourceOptionId, item.TargetOptionId, item.Type, item.Rationale })
            .OrderBy(item => item.SourceOptionId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetOptionId, StringComparer.Ordinal)
            .ThenBy(item => item.Type.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static DecisionTradeoff[] BuildTradeoffs(IReadOnlyList<AnalyzedDecisionOption> analyzedOptions)
    {
        return analyzedOptions
            .OrderBy(option => option.OptionId, StringComparer.Ordinal)
            .Select(option => new DecisionTradeoff(
                option.OptionId,
                option.Benefits
                    .OrderByDescending(benefit => benefit.Impact)
                    .ThenBy(benefit => benefit.Statement, StringComparer.Ordinal)
                    .FirstOrDefault()?.Statement ?? "No regenerated benefit dominated.",
                option.Costs
                    .OrderByDescending(cost => cost.Impact)
                    .ThenBy(cost => cost.Statement, StringComparer.Ordinal)
                    .FirstOrDefault()?.Statement ?? "No regenerated cost dominated.",
                option.Evidence))
            .ToArray();
    }

    private static DecisionGenerationDiagnostics BuildGenerationDiagnostics(
        DecisionGenerationDiagnostics? previous,
        RefinementPlan plan,
        int optionCount,
        int previousOptionCount)
    {
        return new DecisionGenerationDiagnostics(
            optionCount,
            optionCount,
            previous?.RejectedOptionCount ?? 0,
            previous?.DeduplicatedOptionCount ?? 0,
            Math.Max(0, optionCount - previousOptionCount),
            previous?.OptionValidationResults ?? [],
            [
                .. previous?.Diagnostics ?? [],
                "Package regenerated from analyzed refinement directives.",
                $"Regenerate options: {plan.RegenerateOptions}.",
                $"Reevaluate tradeoffs: {plan.ReevaluateTradeoffs}.",
                $"Reevaluate recommendation: {plan.ReevaluateRecommendation}.",
                $"Full regeneration: {plan.FullRegeneration}."
            ]);
    }

    private static DecisionTradeoffAnalysisDiagnostics? BuildTradeoffDiagnostics(
        DecisionTradeoffAnalysisDiagnostics? previous,
        RefinementPlan plan,
        int analyzedOptionCount,
        string contextFingerprint)
    {
        if (!plan.ReevaluateTradeoffs && !plan.RegenerateOptions && !plan.FullRegeneration)
        {
            return previous;
        }

        return new DecisionTradeoffAnalysisDiagnostics(
            analyzedOptionCount,
            contextFingerprint,
            previous?.Unknowns ?? [],
            previous?.ValidationWarnings ?? [],
            [
                .. previous?.Diagnostics ?? [],
                "Tradeoff analysis regenerated from analyzed refinement plan.",
                $"Applied constraints: {string.Join("; ", plan.AppliedConstraints.Order(StringComparer.Ordinal))}."
            ]);
    }

    private static string NextOptionId(IReadOnlyList<DecisionOption> options)
    {
        int next = options.Select(option => OptionNumber(option.Id)).DefaultIfEmpty(0).Max() + 1;
        return $"option-{next}";
    }

    private static int OptionNumber(string optionId)
    {
        string suffix = optionId.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return int.TryParse(suffix, out int value) ? value : 0;
    }

    private static bool SameId(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static IEnumerable<string> EvidenceKeys(DecisionPackage package)
    {
        foreach (DecisionEvidence evidence in package.Evidence)
        {
            yield return EvidenceKey(evidence);
        }

        foreach (DecisionOption option in package.Options)
        {
            foreach (DecisionEvidence evidence in option.Evidence)
            {
                yield return EvidenceKey(evidence);
            }
        }

        if (package.Recommendation is not null)
        {
            foreach (DecisionEvidence evidence in package.Recommendation.Evidence)
            {
                yield return EvidenceKey(evidence);
            }

            foreach (RecommendationEvidence evidence in package.Recommendation.RecommendationEvidence)
            {
                yield return $"{evidence.Type}|{Normalize(evidence.OptionId)}|{Normalize(evidence.Summary)}";
                foreach (DecisionEvidence nestedEvidence in evidence.Evidence)
                {
                    yield return EvidenceKey(nestedEvidence);
                }
            }
        }

        foreach (AnalyzedDecisionOption analyzedOption in package.AnalyzedOptions)
        {
            foreach (DecisionEvidence evidence in analyzedOption.Evidence)
            {
                yield return EvidenceKey(evidence);
            }

            foreach (DecisionBenefit benefit in analyzedOption.Benefits)
            {
                foreach (DecisionEvidence evidence in benefit.Evidence)
                {
                    yield return EvidenceKey(evidence);
                }
            }

            foreach (DecisionCost cost in analyzedOption.Costs)
            {
                foreach (DecisionEvidence evidence in cost.Evidence)
                {
                    yield return EvidenceKey(evidence);
                }
            }

            foreach (DecisionRisk risk in analyzedOption.Risks)
            {
                foreach (DecisionEvidence evidence in risk.Evidence)
                {
                    yield return EvidenceKey(evidence);
                }
            }
        }
    }

    private static IEnumerable<string> RiskKeys(DecisionPackage package)
    {
        foreach (AnalyzedDecisionOption analyzedOption in package.AnalyzedOptions)
        {
            foreach (DecisionRisk risk in analyzedOption.Risks)
            {
                yield return $"{analyzedOption.OptionId}|{Normalize(risk.Statement)}|{risk.Severity}|{risk.IsUnknown}";
            }
        }
    }

    private static string EvidenceKey(DecisionEvidence evidence)
    {
        string sources = string.Join(
            ",",
            evidence.Sources
                .Select(source => $"{Normalize(source.SourceKind)}:{Normalize(source.RelativePath)}:{Normalize(source.Section)}:{Normalize(source.ItemId)}:{Normalize(source.Excerpt)}")
                .Order(StringComparer.Ordinal));
        return $"{Normalize(evidence.Summary)}|{sources}";
    }

    private static string RecommendationSummary(DecisionRecommendation? recommendation)
    {
        return recommendation is null
            ? "None"
            : $"{recommendation.Mode}:{recommendation.OptionId}:{recommendation.Summary}:{recommendation.Rationale}";
    }

    private static string OptionSummary(IReadOnlyList<DecisionOption> options)
    {
        return string.Join(
            ", ",
            options
                .OrderBy(option => option.Id, StringComparer.Ordinal)
                .Select(option => $"{option.Id}:{option.Title}"));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string Fingerprint<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

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
    IDecisionArtifactProjectionService projectionService) : IDecisionPackageService
{
    private const string GeneratorVersion = "deterministic-package-v1";
    private const string MilestoneId = "M6";
    private const string MilestonePath = ".agents/milestones/m6-decision-packages.md";

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

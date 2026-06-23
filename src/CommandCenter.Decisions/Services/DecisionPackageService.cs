using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;

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

        await decisionRepository.SavePackageVersionAsync(repository, packageVersion);
        await projectionService.ProjectPackageVersionAsync(repository, packageVersion);
        return packageVersion;
    }

    private static string Fingerprint<T>(T value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

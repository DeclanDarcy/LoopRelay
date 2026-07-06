using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionPackageService
{
    Task<DecisionPackageVersion> CreatePackageAsync(
        Repository repository,
        DecisionCandidate candidate,
        DecisionProposal proposal,
        DecisionGenerationContext generationContext,
        DateTimeOffset generatedAt);

    Task<DecisionPackageRegenerationResult> RegeneratePackageAsync(
        Repository repository,
        DecisionProposal proposal,
        DecisionPackageVersion basePackageVersion,
        DecisionPackageRegenerationRequest request,
        DateTimeOffset generatedAt);

    DecisionPackageValidationResult ValidatePackage(DecisionPackage package);

    DecisionPackageComparison ComparePackages(DecisionPackageVersion left, DecisionPackageVersion right);
}

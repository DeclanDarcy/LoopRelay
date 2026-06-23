using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionPackageService
{
    Task<DecisionPackageVersion> CreatePackageAsync(
        Repository repository,
        DecisionCandidate candidate,
        DecisionProposal proposal,
        DecisionGenerationContext generationContext,
        DateTimeOffset generatedAt);

    DecisionPackageValidationResult ValidatePackage(DecisionPackage package);
}

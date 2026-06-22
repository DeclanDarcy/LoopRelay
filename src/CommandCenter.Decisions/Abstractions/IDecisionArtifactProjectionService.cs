using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionArtifactProjectionService
{
    Task ProjectDecisionAsync(Repository repository, Decision decision);

    Task ProjectCandidateAsync(Repository repository, DecisionCandidate candidate);

    Task ProjectProposalAsync(Repository repository, DecisionProposal proposal);

    Task ProjectProposalRevisionAsync(Repository repository, DecisionProposalRevision revision);

    Task RefreshDecisionIndexAsync(Repository repository);

    Task RefreshAllAsync(Repository repository);

    Task RecoverMissingProjectionsAsync(Repository repository);
}

using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionResolutionService
{
    Task<Decision> ResolveProposalAsync(Guid repositoryId, string proposalId, ResolveDecisionCommand command);

    Task<Decision> SupersedeDecisionAsync(Guid repositoryId, string decisionId, SupersedeDecisionCommand command);

    Task<Decision> ArchiveDecisionAsync(Guid repositoryId, string decisionId, ArchiveDecisionCommand command);
}

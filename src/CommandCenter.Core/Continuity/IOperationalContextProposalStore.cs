using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Continuity;

public interface IOperationalContextProposalStore
{
    Task<OperationalContextProposal> SaveAsync(Repository repository, OperationalContextProposal proposal, string generatedContent);

    Task<IReadOnlyList<OperationalContextProposal>> ListAsync(Repository repository, bool includeContent = false);

    Task<OperationalContextProposal?> GetAsync(Repository repository, string proposalId, bool includeContent = false);

    Task<OperationalContextProposal> UpdateAsync(
        Repository repository,
        OperationalContextProposal proposal,
        string? editedContent = null,
        bool includeContent = false);

    Task SupersedePendingAsync(Repository repository);
}

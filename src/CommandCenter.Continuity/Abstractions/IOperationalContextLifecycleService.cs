using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IOperationalContextLifecycleService
{
    Task<OperationalContextProposal> PromoteAsync(Guid repositoryId, string proposalId);
}

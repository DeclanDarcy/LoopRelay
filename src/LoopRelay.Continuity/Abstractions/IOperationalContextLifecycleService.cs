using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IOperationalContextLifecycleService
{
    Task<OperationalContextProposal> PromoteAsync(Guid repositoryId, string proposalId);
}

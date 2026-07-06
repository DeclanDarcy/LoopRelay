using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IOperationalContextGenerationService
{
    Task<OperationalContextProposal> GenerateAsync(Guid repositoryId);
}

using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IOperationalContextGenerationService
{
    Task<OperationalContextProposal> GenerateAsync(Guid repositoryId);
}

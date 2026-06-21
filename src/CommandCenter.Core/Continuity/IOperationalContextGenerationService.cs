namespace CommandCenter.Core.Continuity;

public interface IOperationalContextGenerationService
{
    Task<OperationalContextProposal> GenerateAsync(Guid repositoryId);
}

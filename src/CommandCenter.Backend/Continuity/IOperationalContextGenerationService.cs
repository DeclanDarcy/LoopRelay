namespace CommandCenter.Backend.Continuity;

public interface IOperationalContextGenerationService
{
    Task<OperationalContextProposal> GenerateAsync(Guid repositoryId);
}

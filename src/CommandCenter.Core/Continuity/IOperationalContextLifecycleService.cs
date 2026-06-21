namespace CommandCenter.Core.Continuity;

public interface IOperationalContextLifecycleService
{
    Task<OperationalContextProposal> PromoteAsync(Guid repositoryId, string proposalId);
}

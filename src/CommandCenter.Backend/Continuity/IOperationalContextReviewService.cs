namespace CommandCenter.Backend.Continuity;

public interface IOperationalContextReviewService
{
    Task<OperationalContextProposal> EditAsync(Guid repositoryId, string proposalId, string content);

    Task<OperationalContextProposal> AcceptAsync(Guid repositoryId, string proposalId, string? reviewNote);

    Task<OperationalContextProposal> RejectAsync(Guid repositoryId, string proposalId, string? reviewNote);
}

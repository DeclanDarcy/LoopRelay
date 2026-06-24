using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionTransferEligibilityService
{
    Task<DecisionSessionTransferEligibilitySnapshot> CheckAsync(Guid repositoryId);
}

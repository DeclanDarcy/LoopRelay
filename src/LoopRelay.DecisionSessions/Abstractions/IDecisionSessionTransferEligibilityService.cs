using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Abstractions;

public interface IDecisionSessionTransferEligibilityService
{
    Task<DecisionSessionTransferEligibilitySnapshot> CheckAsync(Guid repositoryId);
}

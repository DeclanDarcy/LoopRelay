using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionLifecycleEligibilityService
{
    Task<DecisionLifecycleEligibilityProjection> GetEligibilityAsync(Guid repositoryId);
}

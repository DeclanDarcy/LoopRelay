using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionLifecycleEligibilityService
{
    Task<DecisionLifecycleEligibilityProjection> GetEligibilityAsync(Guid repositoryId);
}

using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionCertificationService
{
    Task<DecisionCertificationReport> GetCurrentCertificationAsync(Guid repositoryId);

    Task<DecisionCertificationReport> RunCertificationAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionCertificationReport>> ListReportsAsync(Guid repositoryId);
}

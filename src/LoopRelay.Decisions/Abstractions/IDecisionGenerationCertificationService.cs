using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionGenerationCertificationService
{
    Task<DecisionGenerationCertificationReport> GetCurrentCertificationAsync(Guid repositoryId);

    Task<DecisionGenerationCertificationReport> RunCertificationAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListReportsAsync(Guid repositoryId);
}

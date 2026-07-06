using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningCertificationService
{
    Task<ReasoningCertificationReport> GetCurrentCertificationAsync(Guid repositoryId);

    Task<ReasoningCertificationReport> RunCertificationAsync(Guid repositoryId);

    Task<IReadOnlyList<ReasoningCertificationReport>> ListReportsAsync(Guid repositoryId);
}

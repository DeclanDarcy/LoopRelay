using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IContinuityReportService
{
    Task<ContinuityReport> GenerateReportAsync(Guid repositoryId);

    Task<IReadOnlyList<ContinuityReport>> ListReportsAsync(Guid repositoryId);
}

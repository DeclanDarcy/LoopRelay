using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IContinuityReportService
{
    Task<ContinuityReport> GenerateReportAsync(Guid repositoryId);

    Task<IReadOnlyList<ContinuityReport>> ListReportsAsync(Guid repositoryId);
}

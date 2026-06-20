namespace CommandCenter.Backend.Continuity;

public interface IContinuityReportService
{
    Task<ContinuityReport> GenerateReportAsync(Guid repositoryId);

    Task<IReadOnlyList<ContinuityReport>> ListReportsAsync(Guid repositoryId);
}

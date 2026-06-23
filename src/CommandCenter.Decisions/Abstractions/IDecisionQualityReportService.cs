using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionQualityReportService
{
    Task<DecisionQualityReport> GenerateReportAsync(Guid repositoryId);

    Task<DecisionQualityReport> GenerateAndSaveReportAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionQualityReport>> ListReportsAsync(Guid repositoryId);

    Task<DecisionQualityReport> SaveReportAsync(Guid repositoryId, DecisionQualityReport report);

    DecisionQualityTrend GenerateTrend(Guid repositoryId, IReadOnlyList<DecisionQualityAssessment> previousAssessments, IReadOnlyList<DecisionQualityAssessment> currentAssessments);

    Task<DecisionQualityTrend> GenerateTrendFromHistoryAsync(Guid repositoryId);

    Task<DecisionQualityTrend> GenerateAndSaveTrendFromHistoryAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionQualityTrend>> ListTrendsAsync(Guid repositoryId);

    Task<DecisionQualityTrend> SaveTrendAsync(Guid repositoryId, DecisionQualityTrend trend);
}

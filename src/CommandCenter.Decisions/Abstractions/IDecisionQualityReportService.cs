using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionQualityReportService
{
    Task<DecisionQualityReport> GenerateReportAsync(Guid repositoryId);

    DecisionQualityTrend GenerateTrend(Guid repositoryId, IReadOnlyList<DecisionQualityAssessment> previousAssessments, IReadOnlyList<DecisionQualityAssessment> currentAssessments);
}

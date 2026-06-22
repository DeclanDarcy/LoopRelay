using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionGovernanceService
{
    Task<DecisionGovernanceReport> GetCurrentReportAsync(Guid repositoryId);

    Task<DecisionGovernanceReport> GenerateReportAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionGovernanceReport>> ListReportsAsync(Guid repositoryId);
}

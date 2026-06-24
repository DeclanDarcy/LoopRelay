using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Abstractions;

public interface IDecisionSessionCertificationService
{
    Task<DecisionSessionCertificationReport?> GetLatestReportAsync(Guid repositoryId);

    Task<DecisionSessionCertificationReport> GetCurrentReportAsync(Guid repositoryId);

    Task<DecisionSessionCertificationReport> RunCertificationAsync(Guid repositoryId);
}

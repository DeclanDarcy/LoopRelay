using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IHumanAuthoringBurdenService
{
    Task<IReadOnlyList<HumanAuthoringBurdenSignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId);

    Task<HumanAuthoringBurdenReport> GenerateReportAsync(Guid repositoryId);
}

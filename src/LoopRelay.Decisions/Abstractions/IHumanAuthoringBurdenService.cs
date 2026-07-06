using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IHumanAuthoringBurdenService
{
    Task<IReadOnlyList<HumanAuthoringBurdenSignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId);

    Task<HumanAuthoringBurdenReport> GenerateReportAsync(Guid repositoryId);
}

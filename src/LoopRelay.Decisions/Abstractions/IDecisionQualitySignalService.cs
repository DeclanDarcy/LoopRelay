using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionQualitySignalService
{
    Task<IReadOnlyList<DecisionQualitySignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId);
}

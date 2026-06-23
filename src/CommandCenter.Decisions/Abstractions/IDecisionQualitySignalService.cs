using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionQualitySignalService
{
    Task<IReadOnlyList<DecisionQualitySignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId);
}

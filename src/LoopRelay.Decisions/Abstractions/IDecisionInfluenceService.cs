using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionInfluenceService
{
    Task<DecisionInfluenceTrace> RecordExecutionInfluenceAsync(
        Guid repositoryId,
        Guid executionSessionId,
        ExecutionDecisionProjection projection);

    Task<DecisionInfluenceTrace?> GetExecutionInfluenceAsync(
        Guid repositoryId,
        Guid executionSessionId);

    Task<IReadOnlyList<DecisionInfluenceTrace>> ListDecisionInfluenceAsync(
        Guid repositoryId,
        string decisionId);
}

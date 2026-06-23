using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionInfluenceService
{
    Task<DecisionInfluenceTrace> RecordExecutionInfluenceAsync(
        Guid repositoryId,
        Guid executionSessionId,
        ExecutionDecisionProjection projection);
}

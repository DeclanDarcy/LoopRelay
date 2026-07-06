using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningQueryService
{
    Task<ReasoningQueryResult> RunQueryAsync(Guid repositoryId, ReasoningQuery query);
}

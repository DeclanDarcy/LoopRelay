using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningQueryService
{
    Task<ReasoningQueryResult> RunQueryAsync(Guid repositoryId, ReasoningQuery query);
}

using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningGraphService
{
    Task<ReasoningGraph> GetGraphAsync(Guid repositoryId);

    Task<ReasoningTrace> TraceBackwardAsync(Guid repositoryId, ReasoningReference target);

    Task<ReasoningTrace> TraceForwardAsync(Guid repositoryId, ReasoningReference target);
}

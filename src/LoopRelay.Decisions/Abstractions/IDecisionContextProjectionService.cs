using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionContextProjectionService
{
    Task<DecisionGenerationContext> BuildGenerationContextAsync(Guid repositoryId);
}

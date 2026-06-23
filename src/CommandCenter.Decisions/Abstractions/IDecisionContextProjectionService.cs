using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionContextProjectionService
{
    Task<DecisionGenerationContext> BuildGenerationContextAsync(Guid repositoryId);
}

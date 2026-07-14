using LoopRelay.Projections.Models;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Projections.Abstractions;

public interface IProjectContextProjectionService
{
    Task<ProjectContextProjectionResult> EnsureFreshAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default);

    Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default);
}

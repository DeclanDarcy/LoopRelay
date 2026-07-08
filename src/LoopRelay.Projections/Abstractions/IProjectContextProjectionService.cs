namespace LoopRelay.Projections;

public interface IProjectContextProjectionService
{
    Task<ProjectContextProjectionResult> EnsureFreshAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default);

    Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default);
}

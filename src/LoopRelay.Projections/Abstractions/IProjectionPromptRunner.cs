namespace LoopRelay.Projections;

public interface IProjectionPromptRunner
{
    Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default);
}

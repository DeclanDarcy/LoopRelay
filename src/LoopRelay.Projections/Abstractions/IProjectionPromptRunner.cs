using LoopRelay.Projections.Models;

namespace LoopRelay.Projections.Abstractions;

public interface IProjectionPromptRunner
{
    Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default);
}

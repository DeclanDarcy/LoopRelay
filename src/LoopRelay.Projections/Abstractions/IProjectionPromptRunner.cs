using LoopRelay.Projections.Models;
using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Projections.Abstractions;

public interface IProjectionPromptRunner
{
    Task<string> RunProjectionPromptAsync(
        ProjectionDefinition definition,
        string prompt,
        CancellationToken cancellationToken = default);
}

using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Abstractions;

internal interface IRoadmapExecutionBridge
{
    Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken);
}

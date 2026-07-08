using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.Execution;

namespace LoopRelay.Roadmap.Cli.Abstractions;

internal interface IRoadmapExecutionBridge
{
    Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken);
}

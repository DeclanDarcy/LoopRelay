using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Infrastructure.Trust;

namespace LoopRelay.Roadmap.Cli;

internal interface IRoadmapExecutionBridge
{
    Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken);
}

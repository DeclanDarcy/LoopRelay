using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Infrastructure.Trust;

namespace LoopRelay.Roadmap.Cli;

internal enum ExecutionTransportStatus
{
    Completed,
    Failed,
}

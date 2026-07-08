using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapCliInvocation(
    RoadmapCliCommand Command,
    Repository Repository,
    RoadmapExecutionOptions ExecutionOptions);

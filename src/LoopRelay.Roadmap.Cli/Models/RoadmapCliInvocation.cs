using LoopRelay.Core.Repositories;

namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapCliInvocation(
    RoadmapCliCommand Command,
    Repository Repository,
    RoadmapExecutionOptions ExecutionOptions);

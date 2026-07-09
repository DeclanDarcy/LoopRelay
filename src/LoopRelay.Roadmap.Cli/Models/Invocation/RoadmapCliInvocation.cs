using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Primitives.State;

namespace LoopRelay.Roadmap.Cli.Models.Invocation;

internal sealed record RoadmapCliInvocation(
    RoadmapCliCommand Command,
    Repository Repository,
    RoadmapExecutionOptions ExecutionOptions,
    RoadmapStorageOptions? StorageOptions = null);

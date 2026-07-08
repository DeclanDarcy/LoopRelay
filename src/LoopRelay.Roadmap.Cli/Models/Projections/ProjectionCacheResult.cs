using LoopRelay.Roadmap.Cli.Primitives.Projections;

namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record ProjectionCacheResult(
    ProjectionDefinition Definition,
    string Content,
    bool Generated,
    ProjectionStaleStatus StaleStatus,
    IReadOnlyList<ProjectionStaleReason> StaleReasons);

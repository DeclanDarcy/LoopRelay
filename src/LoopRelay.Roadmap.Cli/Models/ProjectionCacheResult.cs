using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ProjectionCacheResult(
    ProjectionDefinition Definition,
    string Content,
    bool Generated,
    ProjectionStaleStatus StaleStatus,
    IReadOnlyList<ProjectionStaleReason> StaleReasons);

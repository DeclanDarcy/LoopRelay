using LoopRelay.Projections.Primitives;

namespace LoopRelay.Projections.Models;

public sealed record ProjectContextProjectionResult(
    ProjectionDefinition Definition,
    string Content,
    bool Generated,
    ProjectionStaleStatus StaleStatus,
    IReadOnlyList<ProjectionStaleReason> StaleReasons);

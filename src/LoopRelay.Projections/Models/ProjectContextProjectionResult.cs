namespace LoopRelay.Projections;

public sealed record ProjectContextProjectionResult(
    ProjectionDefinition Definition,
    string Content,
    bool Generated,
    ProjectionStaleStatus StaleStatus,
    IReadOnlyList<ProjectionStaleReason> StaleReasons);

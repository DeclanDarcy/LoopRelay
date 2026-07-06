namespace LoopRelay.Decisions.Persistence;

internal sealed record DecisionArtifactDocument<T>(
    string SchemaVersion,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    T Payload);

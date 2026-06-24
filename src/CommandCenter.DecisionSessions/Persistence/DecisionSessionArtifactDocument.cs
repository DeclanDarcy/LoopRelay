namespace CommandCenter.DecisionSessions.Persistence;

public sealed record DecisionSessionArtifactDocument<T>(
    string SchemaVersion,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    T Payload);

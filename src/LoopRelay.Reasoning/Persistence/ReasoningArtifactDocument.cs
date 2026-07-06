namespace LoopRelay.Reasoning.Persistence;

public sealed record ReasoningArtifactDocument<T>(
    string SchemaVersion,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    T Payload);

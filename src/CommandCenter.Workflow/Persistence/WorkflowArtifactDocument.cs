namespace CommandCenter.Workflow.Persistence;

public sealed record WorkflowArtifactDocument<T>(
    string SchemaVersion,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    T Payload);

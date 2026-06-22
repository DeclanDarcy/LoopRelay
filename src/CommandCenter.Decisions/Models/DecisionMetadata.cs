namespace CommandCenter.Decisions.Models;

public sealed record DecisionMetadata(
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string SchemaVersion = "1");

namespace LoopRelay.Core.Repositories;

public enum RepositoryAvailability
{
    Available,
    Missing,
    AccessDenied
}

public sealed class Repository
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Path { get; init; } = "";
}

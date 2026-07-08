namespace LoopRelay.Core.Models.Repositories;

public sealed class Repository
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Path { get; init; } = "";
}

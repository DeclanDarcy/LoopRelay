using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Configuration;

public sealed class ApplicationConfiguration
{
    public IReadOnlyList<Repository> Repositories { get; init; } = [];
}

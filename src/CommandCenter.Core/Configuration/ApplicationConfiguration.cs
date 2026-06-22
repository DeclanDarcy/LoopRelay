using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Configuration;

public sealed class ApplicationConfiguration
{
    public IReadOnlyList<Repository> Repositories { get; init; } = [];
}

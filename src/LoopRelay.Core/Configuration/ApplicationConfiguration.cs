using LoopRelay.Core.Repositories;

namespace LoopRelay.Core.Configuration;

public sealed class ApplicationConfiguration
{
    public IReadOnlyList<Repository> Repositories { get; init; } = [];
}

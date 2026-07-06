using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Artifacts;

namespace LoopRelay.Infrastructure.Tests;

public sealed class RepositoryArtifactStoreTests
{
    [Fact]
    public async Task WriteReadAndListUseRepositoryRelativePaths()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() };
        var artifacts = new RepositoryArtifactStore(store, repository);

        await artifacts.WriteAsync(".agents/evidence/e0001.md", "EVIDENCE");

        Assert.Equal("EVIDENCE", await artifacts.ReadAsync(".agents/evidence/e0001.md"));
        Assert.Equal([".agents/evidence/e0001.md"], await artifacts.ListAsync(".agents/evidence", "*.md"));
    }

    [Fact]
    public void ResolveRejectsPathsOutsideRepository()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() };
        var artifacts = new RepositoryArtifactStore(store, repository);

        Assert.Throws<ArgumentException>(() => artifacts.Resolve("../outside.md"));
    }

    private static string Root() =>
        Path.Combine(Path.GetTempPath(), "LoopRelay-infrastructure-tests", Guid.NewGuid().ToString("N"));
}

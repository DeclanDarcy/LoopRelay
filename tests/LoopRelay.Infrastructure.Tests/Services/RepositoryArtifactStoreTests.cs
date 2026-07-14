using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Artifacts;

namespace LoopRelay.Infrastructure.Tests.Services;

public sealed class RepositoryArtifactStoreTests
{
    [Fact]
    public async Task RelativeArtifactsAreWrittenUnderTheSelectedRepository()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-scope").FullName;
        try
        {
            var artifacts = new RepositoryArtifactStore(
                new FileSystemArtifactStore(),
                new Repository { Id = Guid.NewGuid(), Name = "repo", Path = root });

            await artifacts.WriteAsync(".agents/review/non-implementation-review.md", "# Review\n");

            string expected = Path.Combine(root, ".agents", "review", "non-implementation-review.md");
            Assert.Equal("# Review\n", await File.ReadAllTextAsync(expected));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
    public async Task RepositoryEscapeIsRejectedBeforeMutation()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() };
        var artifacts = new RepositoryArtifactStore(store, repository);

        await Assert.ThrowsAsync<ArgumentException>(() => artifacts.WriteAsync("../outside.md", "no"));
        Assert.Null(await store.ReadAsync(Path.GetFullPath(Path.Combine(repository.Path, "..", "outside.md"))));
    }

    [Fact]
    public async Task AbsolutePathsAreRejectedAtTheRepositoryBoundary()
    {
        var artifacts = new RepositoryArtifactStore(
            new MemoryArtifactStore(),
            new Repository { Id = Guid.NewGuid(), Name = "repo", Path = Root() });

        await Assert.ThrowsAsync<ArgumentException>(() => artifacts.ReadAsync(Path.GetFullPath("outside.md")));
    }

    [Fact]
    public async Task FilesystemLinksCannotRedirectWritesOutsideTheRepository()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-root").FullName;
        string outside = Directory.CreateTempSubdirectory("looprelay-artifact-outside").FullName;
        try
        {
            string link = Path.Combine(root, "linked");
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // Symlink creation is unavailable on some Windows hosts. Production containment still
                // executes for links and junctions wherever the filesystem permits them.
                return;
            }

            var artifacts = new RepositoryArtifactStore(
                new FileSystemArtifactStore(),
                new Repository { Id = Guid.NewGuid(), Name = "repo", Path = root });

            await Assert.ThrowsAsync<ArgumentException>(() => artifacts.WriteAsync("linked/escaped.md", "no"));
            Assert.False(File.Exists(Path.Combine(outside, "escaped.md")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string Root() =>
        Path.Combine(Path.GetTempPath(), "LoopRelay-infrastructure-tests", Guid.NewGuid().ToString("N"));
}

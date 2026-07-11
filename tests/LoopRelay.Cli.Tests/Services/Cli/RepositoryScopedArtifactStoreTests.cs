using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Services.Artifacts;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class RepositoryScopedArtifactStoreTests
{
    [Fact]
    public async Task Relative_artifacts_are_written_under_the_selected_repository()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-scope").FullName;
        try
        {
            var store = new RepositoryScopedArtifactStore(new FileSystemArtifactStore(), root);

            await store.WriteAsync(".agents/review/non-implementation-review.md", "# Review\n");

            string expected = Path.Combine(root, ".agents", "review", "non-implementation-review.md");
            Assert.True(File.Exists(expected));
            Assert.Equal("# Review\n", await File.ReadAllTextAsync(expected));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Artifact_paths_cannot_escape_the_selected_repository()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-artifact-scope").FullName;
        try
        {
            var store = new RepositoryScopedArtifactStore(new FileSystemArtifactStore(), root);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.WriteAsync("../escaped.md", "no"));

            Assert.Contains("escaped repository root", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

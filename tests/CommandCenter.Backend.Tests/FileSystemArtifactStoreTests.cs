using CommandCenter.Backend.Artifacts;

namespace CommandCenter.Backend.Tests;

public sealed class FileSystemArtifactStoreTests
{
    [Fact]
    public async Task WriteReadExistsAndDelete()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, ".agents", "handoffs", "handoff.md");
        var store = new FileSystemArtifactStore();

        await store.WriteAsync(path, "handoff");

        Assert.True(await store.ExistsAsync(path));
        Assert.Equal("handoff", await store.ReadAsync(path));

        await store.DeleteAsync(path);

        Assert.False(await store.ExistsAsync(path));
    }

    [Fact]
    public async Task WrittenContentPersistsAcrossStoreInstances()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, ".agents", "decisions", "decisions.md");

        await new FileSystemArtifactStore().WriteAsync(path, "decisions");

        Assert.Equal("decisions", await new FileSystemArtifactStore().ReadAsync(path));
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

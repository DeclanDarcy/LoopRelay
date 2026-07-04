using CommandCenter.Core.Artifacts;

namespace CommandCenter.Core.Tests;

public sealed class MemoryArtifactStoreTests
{
    [Fact]
    public async Task WriteReadExistsAndDelete()
    {
        var store = new MemoryArtifactStore();
        string path = Path.Combine("repo", ".agents", "plan.md");

        await store.WriteAsync(path, "plan");

        Assert.True(await store.ExistsAsync(path));
        Assert.Equal("plan", await store.ReadAsync(path));

        await store.DeleteAsync(path);

        Assert.False(await store.ExistsAsync(path));
        Assert.Null(await store.ReadAsync(path));
    }
}

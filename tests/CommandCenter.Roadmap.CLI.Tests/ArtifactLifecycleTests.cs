using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ArtifactLifecycleTests
{
    [Fact]
    public async Task Lifecycle_store_records_ready_and_executing_artifacts()
    {
        using var repo = new TempRepo();
        var store = new ArtifactLifecycleStore(repo.Artifacts);

        await store.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);
        await store.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Executing);

        IReadOnlyList<ArtifactLifecycleEntry> entries = await store.LoadAsync();
        Assert.Single(entries);
        Assert.Equal(ArtifactLifecycleState.Executing, entries[0].State);
    }
}

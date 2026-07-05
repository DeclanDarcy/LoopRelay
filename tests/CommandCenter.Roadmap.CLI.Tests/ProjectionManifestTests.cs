using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ProjectionManifestTests
{
    [Fact]
    public async Task Store_round_trips_manifest_entry()
    {
        using var repo = new TempRepo();
        var store = new ProjectionManifestStore(repo.Artifacts);
        var entry = new ProjectionManifestEntry(
            "SelectNextEpic",
            "ProjectionForSelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            "prompt-hash",
            RoadmapArtifactPaths.NorthStarSourceFiles,
            "north-hash",
            "projection-hash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Fresh,
            null);

        await store.UpsertAsync(entry);

        ProjectionManifest loaded = await store.LoadAsync();
        Assert.Equal("projection-hash", loaded.Find("SelectNextEpic")?.ProjectionHash);
    }

    [Fact]
    public void Manifest_can_mark_stale_hashes()
    {
        var entry = new ProjectionManifestEntry("SelectNextEpic", "ProjectionForSelectNextEpic", "path", "p", [], "old", "h", DateTimeOffset.UtcNow, ProjectionValidationStatus.Valid, ProjectionStaleStatus.Stale, null);
        var manifest = ProjectionManifest.Empty.Upsert(entry);

        Assert.Equal(ProjectionStaleStatus.Stale, manifest.Find("SelectNextEpic")?.StaleStatus);
    }
}

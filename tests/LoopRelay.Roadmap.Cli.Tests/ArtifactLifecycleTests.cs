using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ArtifactLifecycleTests
{
    [Fact]
    public async Task Lifecycle_store_records_ready_and_executing_artifacts()
    {
        using var repo = new TempRepo();
        var store = new Cli.ArtifactLifecycleStore(repo.Artifacts);

        await store.UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready);
        await store.UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Executing);

        IReadOnlyList<Cli.ArtifactLifecycleEntry> entries = await store.LoadAsync();
        Assert.Single(entries);
        Assert.Equal(Cli.ArtifactLifecycleState.Executing, entries[0].State);
        Assert.Contains("\"SchemaVersion\": \"artifact-lifecycle.v1\"", repo.Read(Cli.RoadmapArtifactPaths.LifecycleJson), StringComparison.Ordinal);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.Lifecycle));
    }

    [Fact]
    public async Task Lifecycle_store_loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        await store.SaveAsync([Entry(".agents/epic|with\\slash.md", Cli.ArtifactLifecycleState.Ready, "canonical note | with pipe")]);
        repo.Write(Cli.RoadmapArtifactPaths.Lifecycle, """
                                                       # Artifact Lifecycle

                                                       | Path | State | Updated At | Notes |
                                                       |---|---|---|---|
                                                       | .agents/wrong.md | Archived | 2026-01-01T00:00:00.0000000+00:00 | wrong |
                                                       """);

        Cli.ArtifactLifecycleEntry loaded = Assert.Single(await store.LoadAsync());

        Assert.Equal(".agents/epic|with\\slash.md", loaded.Path);
        Assert.Equal(Cli.ArtifactLifecycleState.Ready, loaded.State);
        Assert.Equal("canonical note | with pipe", loaded.Notes);
    }

    [Fact]
    public async Task Lifecycle_store_round_trips_delimiter_bearing_values()
    {
        using var repo = new TempRepo();
        var store = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        string notes = "line 1\nline 2 | pipe \\ slash <br> literal";

        await store.SaveAsync([Entry(".agents/specs/a|b\\c.md", Cli.ArtifactLifecycleState.Blocked, notes)]);

        Cli.ArtifactLifecycleEntry loaded = Assert.Single(await store.LoadAsync());
        Assert.Equal(".agents/specs/a|b\\c.md", loaded.Path);
        Assert.Equal(notes, loaded.Notes);
    }

    [Fact]
    public async Task Lifecycle_store_json_persistence_is_deterministic_for_same_entries()
    {
        using var repo = new TempRepo();
        var store = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        Cli.ArtifactLifecycleEntry[] entries =
        [
            Entry(".agents/b.md", Cli.ArtifactLifecycleState.Ready, "b"),
            Entry(".agents/a.md", Cli.ArtifactLifecycleState.Draft, "a"),
        ];

        await store.SaveAsync(entries);
        string firstJson = repo.Read(Cli.RoadmapArtifactPaths.LifecycleJson);

        await store.SaveAsync(entries);

        Assert.Equal(firstJson, repo.Read(Cli.RoadmapArtifactPaths.LifecycleJson));
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.Lifecycle));
    }

    [Fact]
    public async Task Lifecycle_store_migrates_valid_legacy_markdown()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.Lifecycle, """
                                                       # Artifact Lifecycle

                                                       | Path | State | Updated At | Notes |
                                                       |---|---|---|---|
                                                       | .agents/epic\|a.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | note with \| pipe |
                                                       """);

        Cli.ArtifactLifecycleEntry loaded = Assert.Single(await new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync());

        Assert.Equal(".agents/epic|a.md", loaded.Path);
        Assert.Equal("note with | pipe", loaded.Notes);
        Assert.True(Exists(repo, Cli.RoadmapArtifactPaths.LifecycleJson));
    }

    [Fact]
    public async Task Lifecycle_store_rejects_malformed_legacy_markdown()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.Lifecycle, """
                                                       # Artifact Lifecycle

                                                       | Path | State | Updated At | Notes |
                                                       |---|---|---|---|
                                                       | .agents/epic|bad.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | note |
                                                       """);

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new Cli.ArtifactLifecycleStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.LifecycleJson));
    }

    private static Cli.ArtifactLifecycleEntry Entry(string path, Cli.ArtifactLifecycleState state, string notes) =>
        new(path, state, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), notes);

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

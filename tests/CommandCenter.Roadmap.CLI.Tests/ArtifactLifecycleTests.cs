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
        Assert.Contains("\"SchemaVersion\": \"artifact-lifecycle.v1\"", repo.Read(RoadmapArtifactPaths.LifecycleJson), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lifecycle_store_loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new ArtifactLifecycleStore(repo.Artifacts);
        await store.SaveAsync([Entry(".agents/epic|with\\slash.md", ArtifactLifecycleState.Ready, "canonical note | with pipe")]);
        repo.Write(RoadmapArtifactPaths.Lifecycle, """
            # Artifact Lifecycle

            | Path | State | Updated At | Notes |
            |---|---|---|---|
            | .agents/wrong.md | Archived | 2026-01-01T00:00:00.0000000+00:00 | wrong |
            """);

        ArtifactLifecycleEntry loaded = Assert.Single(await store.LoadAsync());

        Assert.Equal(".agents/epic|with\\slash.md", loaded.Path);
        Assert.Equal(ArtifactLifecycleState.Ready, loaded.State);
        Assert.Equal("canonical note | with pipe", loaded.Notes);
    }

    [Fact]
    public async Task Lifecycle_store_round_trips_delimiter_bearing_values()
    {
        using var repo = new TempRepo();
        var store = new ArtifactLifecycleStore(repo.Artifacts);
        string notes = "line 1\nline 2 | pipe \\ slash <br> literal";

        await store.SaveAsync([Entry(".agents/specs/a|b\\c.md", ArtifactLifecycleState.Blocked, notes)]);

        ArtifactLifecycleEntry loaded = Assert.Single(await store.LoadAsync());
        Assert.Equal(".agents/specs/a|b\\c.md", loaded.Path);
        Assert.Equal(notes, loaded.Notes);
    }

    [Fact]
    public async Task Lifecycle_store_rendering_is_deterministic_for_same_entries()
    {
        using var repo = new TempRepo();
        var store = new ArtifactLifecycleStore(repo.Artifacts);
        ArtifactLifecycleEntry[] entries =
        [
            Entry(".agents/b.md", ArtifactLifecycleState.Ready, "b"),
            Entry(".agents/a.md", ArtifactLifecycleState.Draft, "a"),
        ];

        await store.SaveAsync(entries);
        string firstJson = repo.Read(RoadmapArtifactPaths.LifecycleJson);
        string firstMarkdown = repo.Read(RoadmapArtifactPaths.Lifecycle);

        await store.SaveAsync(entries);

        Assert.Equal(firstJson, repo.Read(RoadmapArtifactPaths.LifecycleJson));
        Assert.Equal(firstMarkdown, repo.Read(RoadmapArtifactPaths.Lifecycle));
    }

    [Fact]
    public async Task Lifecycle_store_migrates_valid_legacy_markdown()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.Lifecycle, """
            # Artifact Lifecycle

            | Path | State | Updated At | Notes |
            |---|---|---|---|
            | .agents/epic\|a.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | note with \| pipe |
            """);

        ArtifactLifecycleEntry loaded = Assert.Single(await new ArtifactLifecycleStore(repo.Artifacts).LoadAsync());

        Assert.Equal(".agents/epic|a.md", loaded.Path);
        Assert.Equal("note with | pipe", loaded.Notes);
        Assert.True(Exists(repo, RoadmapArtifactPaths.LifecycleJson));
    }

    [Fact]
    public async Task Lifecycle_store_rejects_malformed_legacy_markdown()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.Lifecycle, """
            # Artifact Lifecycle

            | Path | State | Updated At | Notes |
            |---|---|---|---|
            | .agents/epic|bad.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | note |
            """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new ArtifactLifecycleStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, RoadmapArtifactPaths.LifecycleJson));
    }

    private static ArtifactLifecycleEntry Entry(string path, ArtifactLifecycleState state, string notes) =>
        new(path, state, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), notes);

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

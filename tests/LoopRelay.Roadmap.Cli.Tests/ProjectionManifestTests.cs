using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ProjectionManifestTests
{
    [Fact]
    public async Task Store_round_trips_manifest_entry()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        Cli.ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionProvenance provenance = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry())
            .Create("SelectNextEpic", context);
        var store = new Cli.ProjectionManifestStore(repo.Artifacts);
        Cli.ProjectionManifestEntry entry = Cli.ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            "projection-hash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionFreshness.Fresh,
            null);

        await store.UpsertAsync(entry);

        Cli.ProjectionManifest loaded = await store.LoadAsync();
        Cli.ProjectionManifestEntry loadedEntry = Assert.IsType<Cli.ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal("projection-hash", loadedEntry.ProjectionHash);
        Assert.Equal(Cli.ProjectionProvenanceStatus.Trusted, loadedEntry.ProvenanceStatus);
        Assert.Equal(provenance.Prompt.SourceHash, loadedEntry.ProjectionPromptSourceHash);
        Assert.Contains(loadedEntry.EffectiveCausalInputs, input =>
            input.Kind == Cli.ProjectionProvenance.ProjectionPromptTemplateInputKind &&
            input.Version == provenance.Prompt.SourceHash);
        Assert.Contains("\"SchemaVersion\": \"projection-manifest.v1\"", repo.Read(Cli.RoadmapArtifactPaths.ProjectionsManifestJson), StringComparison.Ordinal);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.ProjectionsManifest));
    }

    [Fact]
    public async Task Store_loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new Cli.ProjectionManifestStore(repo.Artifacts);
        await store.SaveAsync(new Cli.ProjectionManifest([EntryWithStructuredValues()]));
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionsManifest, """
                                                                 # Projection Manifest

                                                                 | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                                 |---|---|---|---|---|---|---|---|---|---|---|
                                                                 | SelectNextEpic | Wrong | wrong.md | wrong | wrong | wrong | wrong | 2026-01-01T00:00:00.0000000+00:00 | Invalid | Stale | Corrupted markdown |
                                                                 """);

        Cli.ProjectionManifest loaded = await store.LoadAsync();

        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal("projection-hash|with\\slash", entry.ProjectionHash);
        Cli.ProjectionCausalInput input = Assert.Single(entry.EffectiveCausalInputs);
        Assert.Equal("Identity\\With|Pipe<br>Literal", input.Identity);
    }

    [Fact]
    public async Task Store_round_trips_delimiter_bearing_structured_values()
    {
        using var repo = new TempRepo();
        var store = new Cli.ProjectionManifestStore(repo.Artifacts);

        await store.SaveAsync(new Cli.ProjectionManifest([EntryWithStructuredValues()]));

        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>((await store.LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(".agents/projections/select|next.md", entry.ProjectionPath);
        Assert.Equal("validation | error\nwith newline", entry.LastValidationError);
        Assert.Equal("Version\\With|Pipe", Assert.Single(entry.EffectiveCausalInputs).Version);
    }

    [Fact]
    public async Task Store_json_persistence_is_deterministic_for_same_manifest()
    {
        using var repo = new TempRepo();
        var store = new Cli.ProjectionManifestStore(repo.Artifacts);
        var manifest = new Cli.ProjectionManifest([EntryWithStructuredValues()]);

        await store.SaveAsync(manifest);
        string firstJson = repo.Read(Cli.RoadmapArtifactPaths.ProjectionsManifestJson);

        await store.SaveAsync(manifest);

        Assert.Equal(firstJson, repo.Read(Cli.RoadmapArtifactPaths.ProjectionsManifestJson));
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.ProjectionsManifest));
    }

    [Fact]
    public void Manifest_can_mark_stale_hashes()
    {
        var entry = new Cli.ProjectionManifestEntry("SelectNextEpic", "ProjectionForSelectNextEpic", "path", "p", [], "old", "h", DateTimeOffset.UtcNow, Cli.ProjectionValidationStatus.Valid, Cli.ProjectionStaleStatus.Stale, null);
        var manifest = Cli.ProjectionManifest.Empty.Upsert(entry);

        Assert.Equal(Cli.ProjectionStaleStatus.Stale, manifest.Find("SelectNextEpic")?.StaleStatus);
    }

    [Fact]
    public async Task Legacy_manifest_rows_load_as_unknown_provenance()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionsManifest, """
                                                                 # Projection Manifest

                                                                 | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                                 |---|---|---|---|---|---|---|---|---|---|---|
                                                                 | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | legacy-prompt-name-hash | .agents/project-context.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                                 """);

        Cli.ProjectionManifest loaded = await new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync();

        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal(Cli.ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(Cli.ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
        Assert.Contains(Cli.ProjectionStaleReason.UnknownProvenance, entry.EffectiveStaleReasons);
        Assert.True(Exists(repo, Cli.RoadmapArtifactPaths.ProjectionsManifestJson));
    }

    [Fact]
    public async Task Malformed_legacy_manifest_fails_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionsManifest, """
                                                                 # Projection Manifest

                                                                 | Runtime Prompt | Projection Identity | Projection Prompt | Projection Prompt Type | Path | Provenance Status | Causal Inputs | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Stale Reasons | Last Validation Error |
                                                                 |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
                                                                 | SelectNextEpic | SelectNextEpic | ProjectionForSelectNextEpic | Planning | .agents/projections/select-next-epic.md | Trusted | {not-json} | source-hash | .agents/ctx/01-purpose.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None | None |
                                                                 """);

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.ProjectionsManifestJson));
    }

    private static Cli.ProjectionManifestEntry EntryWithStructuredValues() =>
        new(
            "SelectNextEpic",
            "ProjectionForSelectNextEpic",
            ".agents/projections/select|next.md",
            "source-hash",
            [".agents/ctx/01-purpose.md", ".agents/ctx/02-capability|model.md"],
            "context-hash",
            "projection-hash|with\\slash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionStaleStatus.Stale,
            "validation | error\nwith newline",
            Cli.ProjectionProvenanceStatus.Trusted,
            "SelectNextEpic",
            "Planning",
            [new Cli.ProjectionCausalInput("Kind|Pipe", "Identity\\With|Pipe<br>Literal", "Version\\With|Pipe")],
            [Cli.ProjectionStaleReason.CausalInputDrift]);

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

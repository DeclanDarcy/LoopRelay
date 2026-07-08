using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class ProjectionManifestTests
{
    [Fact]
    public async Task Store_round_trips_manifest_entry()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext context = await new Cli.Services.ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create("SelectNextEpic", context);
        var store = new ProjectionManifestStore(repo.Artifacts);
        ProjectionManifestEntry entry = ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            "projection-hash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ProjectionValidationStatus.Valid,
            ProjectionFreshness.Fresh,
            null);

        await store.UpsertAsync(entry);

        ProjectionManifest loaded = await store.LoadAsync();
        ProjectionManifestEntry loadedEntry = Assert.IsType<ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal("projection-hash", loadedEntry.ProjectionHash);
        Assert.Equal(ProjectionProvenanceStatus.Trusted, loadedEntry.ProvenanceStatus);
        Assert.Equal(provenance.Prompt.SourceHash, loadedEntry.ProjectionPromptSourceHash);
        Assert.Contains(loadedEntry.EffectiveCausalInputs, input =>
            input.Kind == ProjectionProvenance.ProjectionPromptTemplateInputKind &&
            input.Version == provenance.Prompt.SourceHash);
        Assert.Contains("\"SchemaVersion\": \"projection-manifest.v1\"", repo.Read(RoadmapArtifactPaths.ProjectionsManifestJson), StringComparison.Ordinal);
        Assert.False(Exists(repo, RoadmapArtifactPaths.ProjectionsManifest));
    }

    [Fact]
    public async Task Store_loads_json_as_authority_when_markdown_projection_drifted()
    {
        using var repo = new TempRepo();
        var store = new ProjectionManifestStore(repo.Artifacts);
        await store.SaveAsync(new ProjectionManifest([EntryWithStructuredValues()]));
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | Wrong | wrong.md | wrong | wrong | wrong | wrong | 2026-01-01T00:00:00.0000000+00:00 | Invalid | Stale | Corrupted markdown |
                                                             """);

        ProjectionManifest loaded = await store.LoadAsync();

        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal("projection-hash|with\\slash", entry.ProjectionHash);
        ProjectionCausalInput input = Assert.Single(entry.EffectiveCausalInputs);
        Assert.Equal("Identity\\With|Pipe<br>Literal", input.Identity);
    }

    [Fact]
    public async Task Store_round_trips_delimiter_bearing_structured_values()
    {
        using var repo = new TempRepo();
        var store = new ProjectionManifestStore(repo.Artifacts);

        await store.SaveAsync(new ProjectionManifest([EntryWithStructuredValues()]));

        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>((await store.LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(".agents/projections/select|next.md", entry.ProjectionPath);
        Assert.Equal("validation | error\nwith newline", entry.LastValidationError);
        Assert.Equal("Version\\With|Pipe", Assert.Single(entry.EffectiveCausalInputs).Version);
    }

    [Fact]
    public async Task Store_json_persistence_is_deterministic_for_same_manifest()
    {
        using var repo = new TempRepo();
        var store = new ProjectionManifestStore(repo.Artifacts);
        var manifest = new ProjectionManifest([EntryWithStructuredValues()]);

        await store.SaveAsync(manifest);
        string firstJson = repo.Read(RoadmapArtifactPaths.ProjectionsManifestJson);

        await store.SaveAsync(manifest);

        Assert.Equal(firstJson, repo.Read(RoadmapArtifactPaths.ProjectionsManifestJson));
        Assert.False(Exists(repo, RoadmapArtifactPaths.ProjectionsManifest));
    }

    [Fact]
    public void Manifest_can_mark_stale_hashes()
    {
        var entry = new ProjectionManifestEntry("SelectNextEpic", "ProjectionForSelectNextEpic", "path", "p", [], "old", "h", DateTimeOffset.UtcNow, ProjectionValidationStatus.Valid, ProjectionStaleStatus.Stale, null);
        var manifest = ProjectionManifest.Empty.Upsert(entry);

        Assert.Equal(ProjectionStaleStatus.Stale, manifest.Find("SelectNextEpic")?.StaleStatus);
    }

    [Fact]
    public async Task Legacy_manifest_rows_load_as_unknown_provenance()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | legacy-prompt-name-hash | .agents/project-context.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                             """);

        ProjectionManifest loaded = await new ProjectionManifestStore(repo.Artifacts).LoadAsync();

        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(loaded.Find("SelectNextEpic"));
        Assert.Equal(ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
        Assert.Contains(ProjectionStaleReason.UnknownProvenance, entry.EffectiveStaleReasons);
        Assert.True(Exists(repo, RoadmapArtifactPaths.ProjectionsManifestJson));
    }

    [Fact]
    public async Task Malformed_legacy_manifest_fails_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Identity | Projection Prompt | Projection Prompt Type | Path | Provenance Status | Causal Inputs | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Stale Reasons | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | SelectNextEpic | ProjectionForSelectNextEpic | Planning | .agents/projections/select-next-epic.md | Trusted | {not-json} | source-hash | .agents/ctx/01-purpose.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None | None |
                                                             """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new ProjectionManifestStore(repo.Artifacts).LoadAsync());

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, RoadmapArtifactPaths.ProjectionsManifestJson));
    }

    private static ProjectionManifestEntry EntryWithStructuredValues() =>
        new(
            "SelectNextEpic",
            "ProjectionForSelectNextEpic",
            ".agents/projections/select|next.md",
            "source-hash",
            [".agents/ctx/01-purpose.md", ".agents/ctx/02-capability|model.md"],
            "context-hash",
            "projection-hash|with\\slash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Stale,
            "validation | error\nwith newline",
            ProjectionProvenanceStatus.Trusted,
            "SelectNextEpic",
            "Planning",
            [new ProjectionCausalInput("Kind|Pipe", "Identity\\With|Pipe<br>Literal", "Version\\With|Pipe")],
            [ProjectionStaleReason.CausalInputDrift]);

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}

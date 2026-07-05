using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ProjectionManifestTests
{
    [Fact]
    public async Task Store_round_trips_manifest_entry()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
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
    }
}

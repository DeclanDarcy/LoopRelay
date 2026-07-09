using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class EmptyOnMalformedManifestConformanceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    [InlineData("{not-json")]
    public async Task Execution_preparation_manifest_loads_empty_without_mutating_compatibility_source(
        string? persistedContent)
    {
        using var repo = new TempRepo();
        if (persistedContent is not null)
        {
            repo.Write(RoadmapArtifactPaths.ExecutionPreparationManifest, persistedContent);
        }

        IExecutionPreparationManifestStore store = new ExecutionPreparationManifestStore(repo.Artifacts);

        ExecutionPreparationManifest manifest = await store.LoadAsync();

        AssertEmptyExecutionPreparationManifest(manifest);
        await AssertUnchangedAsync(repo, RoadmapArtifactPaths.ExecutionPreparationManifest, persistedContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    [InlineData("{not-json")]
    public async Task Selection_provenance_manifest_loads_empty_without_mutating_compatibility_source(
        string? persistedContent)
    {
        using var repo = new TempRepo();
        if (persistedContent is not null)
        {
            repo.Write(RoadmapArtifactPaths.SelectionProvenanceManifest, persistedContent);
        }

        ISelectionProvenanceManifestStore store = new SelectionProvenanceManifestStore(repo.Artifacts);

        SelectionProvenanceManifest manifest = await store.LoadAsync();

        AssertEmptySelectionProvenanceManifest(manifest);
        await AssertUnchangedAsync(repo, RoadmapArtifactPaths.SelectionProvenanceManifest, persistedContent);
    }

    [Fact]
    public async Task Execution_preparation_manifest_can_be_explicitly_saved_after_malformed_load()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ExecutionPreparationManifest, "{not-json");
        IExecutionPreparationManifestStore store = new ExecutionPreparationManifestStore(repo.Artifacts);

        AssertEmptyExecutionPreparationManifest(await store.LoadAsync());

        var expected = new ExecutionPreparationManifest(
            ExecutionPreparationManifest.CurrentSchemaVersion,
            RoadmapArtifactPaths.ActiveEpic,
            "active-epic-hash",
            [new ExecutionPreparationManifestInput("MilestoneSpec", ".agents/specs/s001.md", "spec-hash")],
            [TrustedEntry("ExecutionPrompt", RoadmapArtifactPaths.ExecutionPrompt)]);

        await store.SaveAsync(expected);

        ExecutionPreparationManifest reloaded = await store.LoadAsync();
        Assert.Equal(RoadmapArtifactPaths.ActiveEpic, reloaded.ActiveEpicPath);
        Assert.Equal("active-epic-hash", reloaded.ActiveEpicHash);
        ExecutionPreparationManifestInput spec = Assert.Single(reloaded.MilestoneSpecs);
        Assert.Equal(".agents/specs/s001.md", spec.Identity);
        DerivedArtifactManifestEntry artifact = Assert.Single(reloaded.ActiveArtifacts);
        Assert.Equal(RoadmapArtifactPaths.ExecutionPrompt, artifact.ArtifactIdentity);
    }

    [Fact]
    public async Task Selection_provenance_manifest_can_be_explicitly_saved_after_malformed_load()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.SelectionProvenanceManifest, "{not-json");
        ISelectionProvenanceManifestStore store = new SelectionProvenanceManifestStore(repo.Artifacts);

        AssertEmptySelectionProvenanceManifest(await store.LoadAsync());

        SelectionProvenanceManifest expected = SelectionProvenanceManifest.Empty.UpsertActive(
            TrustedEntry("Selection", RoadmapArtifactPaths.Selection));

        await store.SaveAsync(expected);

        SelectionProvenanceManifest reloaded = await store.LoadAsync();
        DerivedArtifactManifestEntry selection = Assert.Single(reloaded.ActiveSelections);
        Assert.Equal("Selection", selection.ArtifactKind);
        Assert.Equal(RoadmapArtifactPaths.Selection, selection.ArtifactIdentity);
    }

    private static DerivedArtifactManifestEntry TrustedEntry(string kind, string identity) =>
        new(
            kind,
            identity,
            identity,
            "test",
            "artifact-hash",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DerivedArtifactProvenanceStatus.Trusted,
            [new DerivedArtifactCausalInput("Input", ".agents/roadmap/001-roadmap.md", "input-hash")],
            DerivedArtifactFreshnessStatus.Fresh,
            []);

    private static void AssertEmptyExecutionPreparationManifest(ExecutionPreparationManifest manifest)
    {
        Assert.Equal(ExecutionPreparationManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Equal(string.Empty, manifest.ActiveEpicPath);
        Assert.Equal(string.Empty, manifest.ActiveEpicHash);
        Assert.Empty(manifest.MilestoneSpecs);
        Assert.Empty(manifest.Artifacts);
        Assert.Empty(manifest.ActiveArtifacts);
    }

    private static void AssertEmptySelectionProvenanceManifest(SelectionProvenanceManifest manifest)
    {
        Assert.Equal(SelectionProvenanceManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Empty(manifest.Selections);
        Assert.Empty(manifest.ActiveSelections);
    }

    private static async Task AssertUnchangedAsync(TempRepo repo, string path, string? expectedContent)
    {
        if (expectedContent is null)
        {
            Assert.False(await repo.Artifacts.ExistsAsync(path));
            return;
        }

        Assert.Equal(expectedContent, repo.Read(path));
    }
}

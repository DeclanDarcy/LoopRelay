using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.Manifests;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Models.Provenance;
using LoopRelay.Projections.Primitives;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Projections.Tests.Services;

/// <summary>
/// PERF-18: the projection manifest must not be rewritten on every loop iteration when nothing changed. These
/// tests exercise the real on-disk store (not the in-memory test double used elsewhere) because the property under
/// test IS the physical write behaviour of the file, driven through <see cref="ProjectContextProjectionService.EnsureAsync"/>
/// exactly as the production loop does.
/// </summary>
public sealed class ProjectionManifestUpsertTests
{
    [Fact]
    public async Task Upsert_UnchangedEntry_DoesNotRewriteFile()
    {
        using DiskHarness h = NewDiskHarness(ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"));
        await SeedProjectContextAsync(h);

        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.AdversarialPlanReview, CancellationToken.None);

        string manifestPath = ManifestAbsolutePath(h.Repository);
        Assert.True(File.Exists(manifestPath));
        byte[] bytesAfterFirst = await File.ReadAllBytesAsync(manifestPath);
        DateTime mtimeAfterFirst = File.GetLastWriteTimeUtc(manifestPath);
        int writesAfterFirst = h.Store.WriteCounts.GetValueOrDefault(manifestPath);
        Assert.Equal(1, writesAfterFirst);

        // Second ensure over completely unchanged inputs: this is the steady-state loop iteration the audit
        // flagged. Nothing about the project context, the projection content, or the manifest entry has changed.
        ProjectContextProjectionResult second = await h.Service.EnsureFreshAsync(
            ProjectionRuntimePromptNames.AdversarialPlanReview,
            CancellationToken.None);

        Assert.False(second.Generated);

        byte[] bytesAfterSecond = await File.ReadAllBytesAsync(manifestPath);
        DateTime mtimeAfterSecond = File.GetLastWriteTimeUtc(manifestPath);
        int writesAfterSecond = h.Store.WriteCounts.GetValueOrDefault(manifestPath);

        Assert.Equal(bytesAfterFirst, bytesAfterSecond);
        Assert.Equal(mtimeAfterFirst, mtimeAfterSecond);

        // Belt-and-braces beyond the mtime assertion above: filesystem mtime granularity can be coarse enough on
        // some platforms/filesystems that two writes within the same tick would show an unchanged mtime even
        // though a physical rewrite occurred. The write-count seam on the store cannot be fooled that way — it
        // increments exactly once per physical WriteAsync call, so this is the assertion that actually proves no
        // second write was ever issued.
        Assert.Equal(writesAfterFirst, writesAfterSecond);
    }

    [Fact]
    public async Task Upsert_ChangedEntry_Writes()
    {
        using DiskHarness h = NewDiskHarness(
            ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"),
            ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"));
        await SeedProjectContextAsync(h);

        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.AdversarialPlanReview, CancellationToken.None);

        string manifestPath = ManifestAbsolutePath(h.Repository);
        byte[] bytesAfterFirst = await File.ReadAllBytesAsync(manifestPath);
        int writesAfterFirst = h.Store.WriteCounts.GetValueOrDefault(manifestPath);

        // Mutate a freshness input (project context drift) so the entry's provenance genuinely changes.
        await h.Artifacts.WriteAsync(ProjectionArtifactPaths.ProjectContextSourceFiles[0], "# Context 1\n\nChanged");

        ProjectContextProjectionResult second = await h.Service.EnsureFreshAsync(
            ProjectionRuntimePromptNames.AdversarialPlanReview,
            CancellationToken.None);

        Assert.True(second.Generated);

        byte[] bytesAfterSecond = await File.ReadAllBytesAsync(manifestPath);
        int writesAfterSecond = h.Store.WriteCounts.GetValueOrDefault(manifestPath);

        Assert.NotEqual(bytesAfterFirst, bytesAfterSecond);
        Assert.Equal(writesAfterFirst + 1, writesAfterSecond);
    }

    [Fact]
    public async Task Ensure_ValidationFailure_StillWritesBeforeThrow()
    {
        using DiskHarness h = NewDiskHarness("# Invalid Projection\n\nMissing every required section.");
        await SeedProjectContextAsync(h);

        ProjectionException exception = await Assert.ThrowsAsync<ProjectionException>(() =>
            h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.AdversarialPlanReview, CancellationToken.None));

        Assert.Contains("validation failed", exception.Message, StringComparison.OrdinalIgnoreCase);

        string manifestPath = ManifestAbsolutePath(h.Repository);
        Assert.True(File.Exists(manifestPath));

        ProjectionManifest manifest = await h.ManifestStore.LoadAsync();
        ProjectionManifestEntry entry = Assert.Single(manifest.Entries);
        Assert.Equal(ProjectionValidationStatus.Invalid, entry.ValidationStatus);
    }

    [Fact]
    public async Task Ensure_StaleBlocked_StillWritesBeforeThrow()
    {
        using DiskHarness h = NewDiskHarness(ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"));
        await SeedProjectContextAsync(h);

        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.AdversarialPlanReview, CancellationToken.None);

        // Drift a context file so the existing projection is stale, then request BlockWhenStale semantics so the
        // service throws instead of regenerating.
        await h.Artifacts.WriteAsync(ProjectionArtifactPaths.ProjectContextSourceFiles[0], "# Context 1\n\nChanged");

        ProjectionException exception = await Assert.ThrowsAsync<ProjectionException>(() =>
            h.Service.EnsureAsync(
                ProjectionRuntimePromptNames.AdversarialPlanReview,
                ProjectionRefreshPolicy.BlockWhenStale,
                CancellationToken.None));

        Assert.Contains("stale", exception.Message, StringComparison.OrdinalIgnoreCase);

        ProjectionManifest manifest = await h.ManifestStore.LoadAsync();
        ProjectionManifestEntry entry = Assert.Single(manifest.Entries);
        Assert.Equal(ProjectionStaleStatus.Stale, entry.StaleStatus);
    }

    [Fact]
    public void ManifestEquality_CoversCollections()
    {
        static ProjectionManifestEntry MakeEntry(
            IReadOnlyList<string> contextFiles,
            IReadOnlyList<ProjectionCausalInput> causalInputs,
            IReadOnlyList<ProjectionStaleReason> staleReasons) =>
            new(
                "RuntimePrompt",
                "ProjectionPrompt",
                "path.md",
                "source-hash",
                contextFiles,
                "context-hash",
                "projection-hash",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ProjectionValidationStatus.Valid,
                ProjectionStaleStatus.Fresh,
                null,
                ProjectionProvenanceStatus.Trusted,
                "identity",
                "prompt-type",
                causalInputs,
                staleReasons);

        ProjectionManifestEntry left = MakeEntry(
            new List<string> { "a.md", "b.md" },
            new List<ProjectionCausalInput> { new("kind", "id", "v1") },
            new List<ProjectionStaleReason> { ProjectionStaleReason.ProjectContextDrift });

        // Distinct collection instances (different runtime types, freshly allocated) holding equal elements in
        // the same order — exactly the shape that record `==` gets wrong.
        ProjectionManifestEntry right = MakeEntry(
            new[] { "a.md", "b.md" },
            new[] { new ProjectionCausalInput("kind", "id", "v1") },
            new[] { ProjectionStaleReason.ProjectContextDrift });

        ProjectionManifest leftManifest = new([left]);
        ProjectionManifest rightManifest = new([right]);

        // The crux finding this task guards against: generated record equality compares the collection-typed
        // fields (ProjectContextFiles/CausalInputs/StaleReasons on the entry, Entries on the manifest) by
        // reference, so two structurally identical manifests built from distinct collection instances are NOT
        // "equal" under record equality even though they represent the same state.
        Assert.False(left.Equals(right));
        Assert.NotEqual(leftManifest, rightManifest);

        // The explicit helper must see through that and treat them as equal.
        Assert.True(ProjectionManifestStore.ManifestEquals(leftManifest, rightManifest));
    }

    // ---- Harness -------------------------------------------------------------------------------------------

    private sealed class CountingArtifactStore(IArtifactStore inner) : IArtifactStore
    {
        public Dictionary<string, int> WriteCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

        public Task<string?> ReadAsync(string path) => inner.ReadAsync(path);

        public Task WriteAsync(string path, string content)
        {
            WriteCounts[path] = WriteCounts.GetValueOrDefault(path) + 1;
            return inner.WriteAsync(path, content);
        }

        public Task DeleteAsync(string path) => inner.DeleteAsync(path);

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) =>
            inner.ListAsync(path, searchPattern);

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
            inner.ListDirectoriesAsync(path);
    }

    private sealed class FakeProjectionPromptRunner(params string[] outputs) : IProjectionPromptRunner
    {
        private readonly Queue<string> outputs = new(outputs);

        public Task<string> RunProjectionPromptAsync(
            ProjectionDefinition definition,
            string prompt,
            CancellationToken cancellationToken = default) => Task.FromResult(outputs.Dequeue());
    }

    private sealed record DiskHarness(
        string RepositoryRoot,
        Repository Repository,
        CountingArtifactStore Store,
        ProjectionArtifacts Artifacts,
        ProjectionManifestStore ManifestStore,
        ProjectContextProjectionService Service) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                Directory.Delete(RepositoryRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup: harmless if the OS hasn't released a handle yet.
            }
        }
    }

    private static DiskHarness NewDiskHarness(params string[] projectionOutputs)
    {
        string root = Directory.CreateTempSubdirectory("looprelay-proj-upsert-").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = "r", Path = root };
        var store = new CountingArtifactStore(new FileSystemArtifactStore());
        var artifacts = new ProjectionArtifacts(store, repository);
        var registry = ProjectionDefinitionRegistry.CreateDefault();
        var manifestStore = new ProjectionManifestStore(artifacts);
        var runner = new FakeProjectionPromptRunner(projectionOutputs);
        var service = new ProjectContextProjectionService(
            artifacts,
            registry,
            manifestStore,
            new ProjectionValidator(registry),
            runner);

        return new DiskHarness(root, repository, store, artifacts, manifestStore, service);
    }

    private static async Task SeedProjectContextAsync(DiskHarness h, string suffix = "")
    {
        int index = 0;
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            index++;
            await h.Artifacts.WriteAsync(path, $"# Context {index}\n\nBody {index}{suffix}");
        }
    }

    private static string ManifestAbsolutePath(Repository repository) =>
        ArtifactPath.ResolveRepositoryPath(repository, ProjectionArtifactPaths.ProjectionsManifestJson);

    private static string ValidProjection(string title, string consumer) =>
        $"""
        {title}

        ## Purpose

        Test purpose.

        ## Authority Boundary

        Test authority.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | {consumer} |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test definition |

        ## Downstream Use Instructions

        Test downstream instructions.

        ## Projection Integrity Checklist

        - Valid.
        """;
}

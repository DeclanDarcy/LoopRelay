using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.Manifests;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Primitives;
using LoopRelay.Projections.Services;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Projections.Tests.Services;

public sealed class ProjectionServiceTests
{
    private sealed record Harness(
        ProjectContextProjectionService Service,
        ProjectionManifestStore ManifestStore,
        ProjectionArtifacts Artifacts,
        MemoryArtifactStore Store,
        Repository Repo,
        FakeProjectionPromptRunner Runner);

    private static Harness New(params string[] projectionOutputs)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new ProjectionArtifacts(store, repo);
        var registry = ProjectionDefinitionRegistry.CreateDefault();
        var manifestStore = new ProjectionManifestStore(artifacts);
        var runner = new FakeProjectionPromptRunner(projectionOutputs);
        var service = new ProjectContextProjectionService(
            artifacts,
            registry,
            manifestStore,
            new ProjectionValidator(registry),
            runner);
        return new Harness(service, manifestStore, artifacts, store, repo, runner);
    }

    private static string Resolve(Repository repo, string rel) =>
        ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static async Task SeedProjectContextAsync(Harness h, string suffix = "")
    {
        int index = 0;
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            index++;
            await h.Store.WriteAsync(Resolve(h.Repo, path), $"# Context {index}\n\nBody {index}{suffix}");
        }
    }

    [Fact]
    public async Task EnsureFreshAsync_WhenProjectionMissing_WritesArtifactAndManifestEntry()
    {
        Harness h = New(ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"));
        await SeedProjectContextAsync(h);

        ProjectContextProjectionResult result = await h.Service.EnsureFreshAsync(
            ProjectionRuntimePromptNames.AdversarialPlanReview,
            CancellationToken.None);

        Assert.True(result.Generated);
        Assert.Equal(
            result.Content,
            await h.Store.ReadAsync(Resolve(h.Repo, ProjectionArtifactPaths.ProjectionPaths[ProjectionRuntimePromptNames.AdversarialPlanReview])));

        ProjectionManifest manifest = await h.ManifestStore.LoadAsync();
        ProjectionManifestEntry entry = Assert.Single(manifest.Entries);
        Assert.Equal(ProjectionRuntimePromptNames.AdversarialPlanReview, entry.RuntimePromptName);
        Assert.Equal(9, entry.ProjectContextFiles.Count);
        Assert.Equal(".agents/ctx/09-eval-details.md", entry.ProjectContextFiles[^1]);
        Assert.Equal("ProjectionForAdversarialPlanReview", entry.ProjectionPromptName);
        Assert.Equal(ProjectionValidationStatus.Valid, entry.ValidationStatus);
        Assert.Equal(ProjectionProvenanceStatus.Trusted, entry.ProvenanceStatus);
        Assert.Equal(ProjectionStaleStatus.Fresh, entry.StaleStatus);
        Assert.Equal(ProjectionHash.Sha256(result.Content), entry.ProjectionHash);
        Assert.Single(h.Runner.Calls);
    }

    [Fact]
    public async Task EnsureFreshAsync_WhenProjectionIsFresh_ReusesWithoutCallingRunner()
    {
        Harness h = New(ValidProjection("# Execution Agent System Prompt Projection", "DecisionSession"));
        await SeedProjectContextAsync(h);
        ProjectContextProjectionResult first = await h.Service.EnsureFreshAsync(
            ProjectionRuntimePromptNames.DecisionSession,
            CancellationToken.None);

        Harness reuse = New();
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            string? content = await h.Store.ReadAsync(Resolve(h.Repo, path));
            await reuse.Store.WriteAsync(Resolve(reuse.Repo, path), content!);
        }

        string projectionPath = ProjectionArtifactPaths.ProjectionPaths[ProjectionRuntimePromptNames.DecisionSession];
        await reuse.Store.WriteAsync(Resolve(reuse.Repo, projectionPath), first.Content);
        await reuse.ManifestStore.SaveAsync(await h.ManifestStore.LoadAsync());

        ProjectContextProjectionResult second = await reuse.Service.EnsureFreshAsync(
            ProjectionRuntimePromptNames.DecisionSession,
            CancellationToken.None);

        Assert.False(second.Generated);
        Assert.Equal(first.Content, second.Content);
        Assert.Empty(reuse.Runner.Calls);
    }

    [Fact]
    public async Task EvaluateFreshnessAsync_WhenProjectContextChanges_ReturnsStale()
    {
        Harness h = New(ValidProjection("# Execution Agent System Prompt Projection", "DecisionSession"));
        await SeedProjectContextAsync(h);
        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.DecisionSession, CancellationToken.None);

        await h.Store.WriteAsync(
            Resolve(h.Repo, ProjectionArtifactPaths.ProjectContextSourceFiles[0]),
            "# Context 1\n\nChanged");

        ProjectionFreshness freshness = await h.Service.EvaluateFreshnessAsync(
            ProjectionRuntimePromptNames.DecisionSession,
            CancellationToken.None);

        Assert.Equal(ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
    }

    [Fact]
    public async Task EvaluateFreshnessAsync_WhenEvalDetailsChanges_ReturnsStale()
    {
        Harness h = New(ValidProjection("# Execution Agent System Prompt Projection", "DecisionSession"));
        await SeedProjectContextAsync(h);
        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.DecisionSession, CancellationToken.None);

        await h.Store.WriteAsync(
            Resolve(h.Repo, ".agents/ctx/09-eval-details.md"),
            "# Context 9\n\nChanged evaluation details");

        ProjectionFreshness freshness = await h.Service.EvaluateFreshnessAsync(
            ProjectionRuntimePromptNames.DecisionSession,
            CancellationToken.None);

        Assert.Equal(ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
    }

    [Fact]
    public void Validator_AcceptsNewConsumers_AndRejectsMissingRequiredSections()
    {
        var registry = ProjectionDefinitionRegistry.CreateDefault();
        var validator = new ProjectionValidator(registry);

        Assert.True(validator.Validate(
            ProjectionRuntimePromptNames.AdversarialPlanReview,
            ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview")).IsValid);
        Assert.True(validator.Validate(
            ProjectionRuntimePromptNames.DecisionSession,
            ValidProjection("# Execution Agent System Prompt Projection", "DecisionSession")).IsValid);
        Assert.True(validator.Validate(
            "EvaluateEpicCompletionAndDrift",
            ValidProjection("# Epic Completion Evaluation Projection", "`EvaluateEpicCompletionAndDrift`")
                .Replace("## Projection Integrity Checklist\n\n- Valid.\n", string.Empty, StringComparison.Ordinal)).IsValid);

        ProjectionValidationResult invalid = validator.Validate(
            ProjectionRuntimePromptNames.DecisionSession,
            ValidProjection("# Execution Agent System Prompt Projection", "DecisionSession")
                .Replace("## Canonical Vocabulary", "## Vocabulary", StringComparison.Ordinal));

        Assert.False(invalid.IsValid);
        Assert.Contains("## Canonical Vocabulary", invalid.Error);
    }

    [Fact]
    public async Task EnsureFreshAsync_PreservesExistingRoadmapManifestEntries()
    {
        Harness h = New(ValidProjection("# Adversarial Plan Review Projection", "AdversarialPlanReview"));
        await SeedProjectContextAsync(h);
        var roadmapEntry = new ProjectionManifestEntry(
            "SelectNextEpic",
            "ProjectionForSelectNextEpic",
            ".agents/projections/select-next-epic.md",
            "prompt-hash",
            ProjectionArtifactPaths.ProjectContextSourceFiles,
            "context-hash",
            "roadmap-projection-hash",
            DateTimeOffset.UtcNow,
            ProjectionValidationStatus.Valid,
            ProjectionStaleStatus.Fresh,
            null,
            ProjectionProvenanceStatus.Trusted,
            "SelectNextEpic",
            "LoopRelay.Core.Prompts.Projections.ProjectionForSelectNextEpic",
            [],
            []);
        await h.ManifestStore.SaveAsync(new ProjectionManifest([roadmapEntry]));

        await h.Service.EnsureFreshAsync(ProjectionRuntimePromptNames.AdversarialPlanReview, CancellationToken.None);

        ProjectionManifest manifest = await h.ManifestStore.LoadAsync();
        Assert.Contains(manifest.Entries, entry => entry.RuntimePromptName == "SelectNextEpic");
        Assert.Contains(manifest.Entries, entry => entry.RuntimePromptName == ProjectionRuntimePromptNames.AdversarialPlanReview);
    }

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

    private sealed class FakeProjectionPromptRunner(params string[] outputs) : IProjectionPromptRunner
    {
        private readonly Queue<string> outputs = new(outputs);

        public List<(ProjectionDefinition Definition, string Prompt)> Calls { get; } = new();

        public Task<string> RunProjectionPromptAsync(
            ProjectionDefinition definition,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((definition, prompt));
            return Task.FromResult(outputs.Dequeue());
        }
    }
}

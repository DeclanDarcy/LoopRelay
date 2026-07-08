using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Projections;

public sealed class ProjectionCacheTests
{
    [Fact]
    public async Task Existing_projection_with_fresh_provenance_does_not_call_runtime()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, projectContext);
        ScriptedAgentRuntime runtime = new();
        ProjectionCache cache = CreateCache(repo, runtime);

        ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None);

        Assert.False(result.Generated);
        Assert.Equal(ProjectionStaleStatus.Fresh, result.StaleStatus);
        Assert.Equal(0, runtime.OneShotCalls);
    }

    [Fact]
    public async Task Missing_projection_calls_runtime_once_and_writes_output()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ScriptedAgentRuntime runtime = new(ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")));
        ProjectionCache cache = CreateCache(repo, runtime);

        ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None);

        Assert.True(result.Generated);
        Assert.Equal(ProjectionStaleStatus.Fresh, result.StaleStatus);
        Assert.Equal(1, runtime.OneShotCalls);
        Assert.Contains("# Select Next Epic Projection", repo.Read(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]), StringComparison.Ordinal);
        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(
            (await new ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(ProjectionProvenanceStatus.Trusted, entry.ProvenanceStatus);
        Assert.Equal(LoopRelay.Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash, entry.ProjectionPromptSourceHash);
    }

    [Fact]
    public async Task Failed_turn_does_not_write_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime(ScriptedAgentRuntime.Failed()));

        await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(repo.Root, RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"].Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Stale_projection_blocks_when_contract_policy_is_block()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, WithProjectContextHash(projectContext, "old-context-hash"));
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));
        Assert.Contains(nameof(ProjectionStaleReason.ProjectContextDrift), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_projection_without_manifest_is_unknown_provenance_and_blocks()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.Contains(nameof(ProjectionStaleReason.MissingManifest), ex.Message, StringComparison.Ordinal);
        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(
            (await new ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
    }

    [Fact]
    public async Task Legacy_manifest_row_does_not_trust_existing_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | legacy-prompt-name-hash | .agents/project-context.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                             """);
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.Contains(nameof(ProjectionStaleReason.UnknownProvenance), ex.Message, StringComparison.Ordinal);
        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(
            (await new ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
    }

    [Fact]
    public async Task Prompt_template_drift_invalidates_cached_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionProvenance current = new ProjectionProvenanceFactory(new ProjectionRegistry()).Create("SelectNextEpic", projectContext);
        await SeedTrustedManifestAsync(repo, WithPromptSourceHash(current, "old-prompt-source-hash"));

        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));
        Assert.Contains(nameof(ProjectionStaleReason.PromptTemplateDrift), ex.Message, StringComparison.Ordinal);

        ProjectionManifestEntry entry = Assert.IsType<ProjectionManifestEntry>(
            (await new ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Contains(ProjectionStaleReason.PromptTemplateDrift, entry.EffectiveStaleReasons);
    }

    [Fact]
    public async Task Allow_policy_reuses_stale_projection_but_records_stale_reason()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, WithProjectContextHash(projectContext, "old-context-hash"));
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());
        PromptContract contract = new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic") with
        {
            StaleProjectionPolicy = StaleProjectionPolicy.Allow,
        };

        ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, contract, CancellationToken.None);

        Assert.False(result.Generated);
        Assert.Equal(ProjectionStaleStatus.Stale, result.StaleStatus);
        Assert.Contains(ProjectionStaleReason.ProjectContextDrift, result.StaleReasons);
    }

    private static ProjectionCache CreateCache(TempRepo repo, ScriptedAgentRuntime runtime)
    {
        var registry = new ProjectionRegistry();
        return new ProjectionCache(
            repo.Artifacts,
            registry,
            new ProjectionManifestStore(repo.Artifacts),
            new ProjectionValidator(),
            new RoadmapPromptRunner(runtime, repo.Repository, new TestConsole()));
    }

    private static async Task SeedTrustedManifestAsync(TempRepo repo, ProjectContext projectContext)
    {
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create("SelectNextEpic", projectContext);
        await SeedTrustedManifestAsync(repo, provenance);
    }

    private static async Task SeedTrustedManifestAsync(TempRepo repo, ProjectionProvenance provenance)
    {
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(
            ProjectionManifestEntry.FromTrustedProvenance(
                provenance,
                "projection-hash",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ProjectionValidationStatus.Valid,
                ProjectionFreshness.Fresh,
                null));
    }

    private static ProjectionProvenance WithProjectContextHash(ProjectContext context, string contextHash)
    {
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create("SelectNextEpic", context);
        return provenance with
        {
            ProjectContextHash = contextHash,
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectContextInputKind
                    ? input with { Version = contextHash }
                    : input).ToArray(),
        };
    }

    private static ProjectionProvenance WithPromptSourceHash(ProjectionProvenance provenance, string sourceHash) =>
        provenance with
        {
            Prompt = provenance.Prompt with { SourceHash = sourceHash },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Version = sourceHash }
                    : input).ToArray(),
        };
}

using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ProjectionCacheTests
{
    [Fact]
    public async Task Existing_projection_with_fresh_provenance_does_not_call_runtime()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, projectContext);
        ScriptedAgentRuntime runtime = new();
        Cli.ProjectionCache cache = CreateCache(repo, runtime);

        Cli.ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None);

        Assert.False(result.Generated);
        Assert.Equal(Cli.ProjectionStaleStatus.Fresh, result.StaleStatus);
        Assert.Equal(0, runtime.OneShotCalls);
    }

    [Fact]
    public async Task Missing_projection_calls_runtime_once_and_writes_output()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ScriptedAgentRuntime runtime = new(ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")));
        Cli.ProjectionCache cache = CreateCache(repo, runtime);

        Cli.ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None);

        Assert.True(result.Generated);
        Assert.Equal(Cli.ProjectionStaleStatus.Fresh, result.StaleStatus);
        Assert.Equal(1, runtime.OneShotCalls);
        Assert.Contains("# Select Next Epic Projection", repo.Read(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]), StringComparison.Ordinal);
        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(
            (await new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(Cli.ProjectionProvenanceStatus.Trusted, entry.ProvenanceStatus);
        Assert.Equal(LoopRelay.Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash, entry.ProjectionPromptSourceHash);
    }

    [Fact]
    public async Task Failed_turn_does_not_write_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime(ScriptedAgentRuntime.Failed()));

        await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(repo.Root, Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"].Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Stale_projection_blocks_when_contract_policy_is_block()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, WithProjectContextHash(projectContext, "old-context-hash"));
        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));
        Assert.Contains(nameof(Cli.ProjectionStaleReason.ProjectContextDrift), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_projection_without_manifest_is_unknown_provenance_and_blocks()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.Contains(nameof(Cli.ProjectionStaleReason.MissingManifest), ex.Message, StringComparison.Ordinal);
        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(
            (await new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(Cli.ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(Cli.ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
    }

    [Fact]
    public async Task Legacy_manifest_row_does_not_trust_existing_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionsManifest, """
                                                                 # Projection Manifest

                                                                 | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                                 |---|---|---|---|---|---|---|---|---|---|---|
                                                                 | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | legacy-prompt-name-hash | .agents/project-context.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                                 """);
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));

        Assert.Contains(nameof(Cli.ProjectionStaleReason.UnknownProvenance), ex.Message, StringComparison.Ordinal);
        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(
            (await new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Equal(Cli.ProjectionProvenanceStatus.Unknown, entry.ProvenanceStatus);
        Assert.Equal(Cli.ProjectionStaleStatus.UnknownProvenance, entry.StaleStatus);
    }

    [Fact]
    public async Task Prompt_template_drift_invalidates_cached_projection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionProvenance current = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry()).Create("SelectNextEpic", projectContext);
        await SeedTrustedManifestAsync(repo, WithPromptSourceHash(current, "old-prompt-source-hash"));

        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));
        Assert.Contains(nameof(Cli.ProjectionStaleReason.PromptTemplateDrift), ex.Message, StringComparison.Ordinal);

        Cli.ProjectionManifestEntry entry = Assert.IsType<Cli.ProjectionManifestEntry>(
            (await new Cli.ProjectionManifestStore(repo.Artifacts).LoadAsync()).Find("SelectNextEpic"));
        Assert.Contains(Cli.ProjectionStaleReason.PromptTemplateDrift, entry.EffectiveStaleReasons);
    }

    [Fact]
    public async Task Allow_policy_reuses_stale_projection_but_records_stale_reason()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        string path = Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        repo.Write(path, ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        await SeedTrustedManifestAsync(repo, WithProjectContextHash(projectContext, "old-context-hash"));
        Cli.ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());
        Cli.PromptContract contract = new Cli.PromptContractRegistry(new Cli.ProjectionRegistry()).Get("SelectNextEpic") with
        {
            StaleProjectionPolicy = Cli.StaleProjectionPolicy.Allow,
        };

        Cli.ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, contract, CancellationToken.None);

        Assert.False(result.Generated);
        Assert.Equal(Cli.ProjectionStaleStatus.Stale, result.StaleStatus);
        Assert.Contains(Cli.ProjectionStaleReason.ProjectContextDrift, result.StaleReasons);
    }

    private static Cli.ProjectionCache CreateCache(TempRepo repo, ScriptedAgentRuntime runtime)
    {
        var registry = new Cli.ProjectionRegistry();
        return new Cli.ProjectionCache(
            repo.Artifacts,
            registry,
            new Cli.ProjectionManifestStore(repo.Artifacts),
            new Cli.ProjectionValidator(),
            new Cli.RoadmapPromptRunner(runtime, repo.Repository, new TestConsole()));
    }

    private static async Task SeedTrustedManifestAsync(TempRepo repo, Cli.ProjectContext projectContext)
    {
        Cli.ProjectionProvenance provenance = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry())
            .Create("SelectNextEpic", projectContext);
        await SeedTrustedManifestAsync(repo, provenance);
    }

    private static async Task SeedTrustedManifestAsync(TempRepo repo, Cli.ProjectionProvenance provenance)
    {
        await new Cli.ProjectionManifestStore(repo.Artifacts).UpsertAsync(
            Cli.ProjectionManifestEntry.FromTrustedProvenance(
                provenance,
                "projection-hash",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                Cli.ProjectionValidationStatus.Valid,
                Cli.ProjectionFreshness.Fresh,
                null));
    }

    private static Cli.ProjectionProvenance WithProjectContextHash(Cli.ProjectContext context, string contextHash)
    {
        Cli.ProjectionProvenance provenance = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry())
            .Create("SelectNextEpic", context);
        return provenance with
        {
            ProjectContextHash = contextHash,
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == Cli.ProjectionProvenance.ProjectContextInputKind
                    ? input with { Version = contextHash }
                    : input).ToArray(),
        };
    }

    private static Cli.ProjectionProvenance WithPromptSourceHash(Cli.ProjectionProvenance provenance, string sourceHash) =>
        provenance with
        {
            Prompt = provenance.Prompt with { SourceHash = sourceHash },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == Cli.ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Version = sourceHash }
                    : input).ToArray(),
        };
}

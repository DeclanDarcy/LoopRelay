using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ProjectionCacheTests
{
    [Fact]
    public async Task Existing_projection_does_not_call_runtime()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ScriptedAgentRuntime runtime = new();
        ProjectionCache cache = CreateCache(repo, runtime);

        ProjectionCacheResult result = await cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None);

        Assert.False(result.Generated);
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
        Assert.Equal(1, runtime.OneShotCalls);
        Assert.Contains("# Select Next Epic Projection", repo.Read(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]), StringComparison.Ordinal);
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
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(new ProjectionManifestEntry("SelectNextEpic", "ProjectionForSelectNextEpic", path, "p", RoadmapArtifactPaths.ProjectContextSourceFiles, "old", "h", DateTimeOffset.UtcNow, ProjectionValidationStatus.Valid, ProjectionStaleStatus.Fresh, null));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionCache cache = CreateCache(repo, new ScriptedAgentRuntime());

        await Assert.ThrowsAsync<RoadmapStepException>(() => cache.EnsureAsync("SelectNextEpic", projectContext, new PromptContractRegistry(new ProjectionRegistry()).Get("SelectNextEpic"), CancellationToken.None));
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
}

using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.PlanArtifactOperations;

public class PlanArtifactsTests
{
    private static (PlanArtifacts Artifacts, IArtifactStore Store, Repository Repo) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new PlanArtifacts(store, repo), store, repo);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Fact]
    public async Task RelativeHelpers_ExistsReadWrite_ResolveThroughRepositoryRoot()
    {
        var (art, store, repo) = New();

        Assert.False(await art.ExistsAsync(OrchestrationArtifactPaths.Plan));
        Assert.Null(await art.ReadAsync(OrchestrationArtifactPaths.Plan));

        await art.WriteAsync(OrchestrationArtifactPaths.Plan, "PLAN");

        Assert.True(await art.ExistsAsync(OrchestrationArtifactPaths.Plan));
        Assert.Equal("PLAN", await art.ReadAsync(OrchestrationArtifactPaths.Plan));
        Assert.Equal("PLAN", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RelativeHelpers_RejectAbsolutePath()
    {
        var (art, _, _) = New();

        await Assert.ThrowsAsync<ArgumentException>(() => art.ExistsAsync("/outside/plan.md"));
    }

    [Fact]
    public async Task AbsoluteHelpers_RoundTrip_OutsideRepositoryRoot()
    {
        var (art, _, _) = New();
        string dir = Path.Combine(Path.GetTempPath(), "cc-plan-cli-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "plan.md");

        Assert.False(await art.ExistsAbsoluteAsync(file));
        Assert.Null(await art.ReadAbsoluteAsync(file));

        await art.WriteAbsoluteAsync(file, "SANDBOX PLAN");

        Assert.True(await art.ExistsAbsoluteAsync(file));
        Assert.Equal("SANDBOX PLAN", await art.ReadAbsoluteAsync(file));

        IReadOnlyList<string> listed = await art.ListAbsoluteAsync(dir, "*.md");
        Assert.Contains(file, listed);
    }

    [Fact]
    public async Task ListSpecsRelative_ReturnsEpicAndSFiles()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Spec(1)), "S1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Spec(2)), "S2");

        IReadOnlyList<string> specs = await art.ListSpecsRelativeAsync();

        Assert.Equal(
            new[]
            {
                OrchestrationArtifactPaths.SpecsEpic,
                OrchestrationArtifactPaths.Spec(1),
                OrchestrationArtifactPaths.Spec(2),
            }.OrderBy(s => s, StringComparer.OrdinalIgnoreCase),
            specs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListSpecsRelative_WhenEmpty_ReturnsEmpty()
    {
        var (art, _, _) = New();

        Assert.Empty(await art.ListSpecsRelativeAsync());
    }

    [Fact]
    public async Task ListMilestonesRelative_MatchesOnlyMStarPattern()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md")),
            "M1");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m2-bar.md")),
            "M2");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "notes.md")),
            "NOTES");

        IReadOnlyList<string> milestones = await art.ListMilestonesRelativeAsync();

        Assert.Equal(2, milestones.Count);
        Assert.Contains(milestones, m => m.EndsWith("m1-foo.md", StringComparison.Ordinal));
        Assert.Contains(milestones, m => m.EndsWith("m2-bar.md", StringComparison.Ordinal));
        Assert.DoesNotContain(milestones, m => m.EndsWith("notes.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListMilestonesRelative_WhenEmpty_ReturnsEmpty()
    {
        var (art, _, _) = New();

        Assert.Empty(await art.ListMilestonesRelativeAsync());
    }

    [Fact]
    public async Task ListMilestonesRelative_ReturnsRepositoryRelativePaths()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1.md")),
            "- [ ] a");

        IReadOnlyList<string> milestones = await art.ListMilestonesRelativeAsync();

        Assert.Equal(
            new[] { ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1.md") },
            milestones);
    }
}

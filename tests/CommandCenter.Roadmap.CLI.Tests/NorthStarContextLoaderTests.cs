using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class NorthStarContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_concatenates_in_fixed_order()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        var loader = new NorthStarContextLoader(repo.Artifacts);

        NorthStarContext context = await loader.LoadAsync();

        Assert.Contains("<!-- BEGIN NORTH-STAR FILE: 01-purpose.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("north star 01", StringComparison.Ordinal) <
                    context.Content.IndexOf("north star 08", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_lists_all_missing_files()
    {
        using var repo = new TempRepo();
        var loader = new NorthStarContextLoader(repo.Artifacts);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => loader.LoadAsync());

        Assert.Contains(".agents/north-star/01-purpose.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/north-star/08-vocabulary.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ignores_runtime_completion_context()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "runtime state");

        NorthStarContext context = await new NorthStarContextLoader(repo.Artifacts).LoadAsync();

        Assert.DoesNotContain("runtime state", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_fails_on_extra_numbered_source_file()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        repo.Write(".agents/north-star/09-extra.md", "extra");

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new NorthStarContextLoader(repo.Artifacts).LoadAsync());

        Assert.Contains("09-extra.md", ex.Message, StringComparison.Ordinal);
    }
}

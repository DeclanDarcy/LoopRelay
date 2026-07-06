using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ProjectContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_concatenates_in_fixed_order()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        var loader = new ProjectContextLoader(repo.Artifacts);

        Cli.ProjectContext context = await loader.LoadAsync();

        Assert.Contains("<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("project context 01", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 08", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_lists_all_missing_files()
    {
        using var repo = new TempRepo();
        var loader = new ProjectContextLoader(repo.Artifacts);

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => loader.LoadAsync());

        Assert.Contains(".agents/ctx/01-purpose.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/08-vocabulary.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ignores_runtime_completion_context()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "runtime state");

        Cli.ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Assert.DoesNotContain("runtime state", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_fails_on_extra_numbered_source_file()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/ctx/09-extra.md", "extra");

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new ProjectContextLoader(repo.Artifacts).LoadAsync());

        Assert.Contains("09-extra.md", ex.Message, StringComparison.Ordinal);
    }
}

using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ProjectContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_concatenates_in_fixed_order()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        var loader = new ProjectContextLoader(repo.Artifacts);

        ProjectContext context = await loader.LoadAsync();

        Assert.Contains("<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("project context 01", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 08", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_lists_all_missing_files()
    {
        using var repo = new TempRepo();
        var loader = new ProjectContextLoader(repo.Artifacts);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => loader.LoadAsync());

        Assert.Contains(".agents/core/01-purpose.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/core/08-vocabulary.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ignores_runtime_completion_context()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "runtime state");

        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Assert.DoesNotContain("runtime state", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_fails_on_extra_numbered_source_file()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/core/09-extra.md", "extra");

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new ProjectContextLoader(repo.Artifacts).LoadAsync());

        Assert.Contains("09-extra.md", ex.Message, StringComparison.Ordinal);
    }
}

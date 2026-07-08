using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class ProjectContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_concatenates_in_fixed_order()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        var loader = new Cli.Services.ProjectContextLoader(repo.Artifacts);

        ProjectContext context = await loader.LoadAsync();

        Assert.Contains("<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("project context 01", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 08", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_lists_all_missing_files()
    {
        using var repo = new TempRepo();
        var loader = new Cli.Services.ProjectContextLoader(repo.Artifacts);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => loader.LoadAsync());

        Assert.Contains(".agents/ctx/01-purpose.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/08-vocabulary.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ignores_runtime_completion_context()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "runtime state");

        ProjectContext context = await new Cli.Services.ProjectContextLoader(repo.Artifacts).LoadAsync();

        Assert.DoesNotContain("runtime state", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_fails_on_extra_numbered_source_file()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/ctx/09-extra.md", "extra");

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new Cli.Services.ProjectContextLoader(repo.Artifacts).LoadAsync());

        Assert.Contains("09-extra.md", ex.Message, StringComparison.Ordinal);
    }
}

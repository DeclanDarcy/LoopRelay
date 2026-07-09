using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Projections;

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
        Assert.Contains("<!-- BEGIN PROJECT-CONTEXT FILE: 09-eval-details.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("project context 01", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 09", StringComparison.Ordinal));
        Assert.True(context.Content.IndexOf("project context 08", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 09", StringComparison.Ordinal));
        Assert.Equal(ProjectContextSourceContract.SourceFiles, context.SourceFiles);
    }

    [Fact]
    public async Task LoadAsync_lists_all_missing_files()
    {
        using var repo = new TempRepo();
        var loader = new ProjectContextLoader(repo.Artifacts);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => loader.LoadAsync());

        Assert.Contains(".agents/ctx/01-purpose.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/08-vocabulary.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/09-eval-details.md", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("01 through 08", ex.Message, StringComparison.Ordinal);
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
        repo.Write(".agents/ctx/09-extra.md", "extra");

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(() => new ProjectContextLoader(repo.Artifacts).LoadAsync());

        Assert.Contains("09-extra.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ignores_non_numbered_markdown_under_context_directory()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/ctx/readme.md", "non canonical notes");

        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Assert.DoesNotContain("non canonical notes", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_hash_changes_when_eval_details_changes()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext before = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        repo.Write(".agents/ctx/09-eval-details.md", "changed evaluation details");
        ProjectContext after = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Assert.NotEqual(before.Hash, after.Hash);
        Assert.Contains("changed evaluation details", after.Content, StringComparison.Ordinal);
    }
}

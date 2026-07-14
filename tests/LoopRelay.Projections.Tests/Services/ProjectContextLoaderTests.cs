using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Projections.Tests.Services;

public sealed class ProjectContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_concatenates_all_nine_sources_in_fixed_order()
    {
        Harness h = NewHarness();
        await SeedProjectContextAsync(h);

        ProjectContext context = await new ProjectContextLoader(h.Artifacts).LoadAsync();

        Assert.Equal(ProjectContextSourceContract.SourceFiles, ProjectionArtifactPaths.ProjectContextSourceFiles);
        Assert.Equal(ProjectContextSourceContract.SourceFiles, context.SourceFiles);
        Assert.Contains("<!-- BEGIN PROJECT-CONTEXT FILE: 09-eval-details.md -->", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf("project context 08", StringComparison.Ordinal) <
                    context.Content.IndexOf("project context 09", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_requires_eval_details_and_rejects_extra_numbered_files()
    {
        Harness h = NewHarness();
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles.Take(8))
        {
            await h.Store.WriteAsync(Resolve(h.Repository, path), "context");
        }

        await h.Store.WriteAsync(Resolve(h.Repository, ".agents/ctx/10-extra.md"), "extra");
        await h.Store.WriteAsync(Resolve(h.Repository, ".agents/ctx/readme.md"), "notes");

        ProjectionException ex = await Assert.ThrowsAsync<ProjectionException>(
            () => new ProjectContextLoader(h.Artifacts).LoadAsync());

        Assert.Contains(".agents/ctx/09-eval-details.md", ex.Message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/10-extra.md", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(".agents/ctx/readme.md", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("01 through 08", ex.Message, StringComparison.Ordinal);
    }

    private static Harness NewHarness()
    {
        var store = new MemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
        return new Harness(store, repository, new ProjectionArtifacts(store, repository));
    }

    private static async Task SeedProjectContextAsync(Harness h)
    {
        int index = 1;
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            await h.Store.WriteAsync(Resolve(h.Repository, path), $"project context {index:00}");
            index++;
        }
    }

    private static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private sealed record Harness(
        MemoryArtifactStore Store,
        Repository Repository,
        ProjectionArtifacts Artifacts);
}

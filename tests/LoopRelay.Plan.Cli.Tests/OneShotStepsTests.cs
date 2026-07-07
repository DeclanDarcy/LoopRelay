using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class OneShotStepsTests
{
    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static (Cli.PlanArtifacts Artifacts, MemoryArtifactStore Store, Repository Repo) NewArtifacts()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new Cli.PlanArtifacts(store, repo), store, repo);
    }

    [Fact]
    public async Task CollectDetailsAsync_BuildsScopedOperation()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Spec(1)), "SPEC 1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        Cli.ArtifactOperationPlan plan = await Cli.OneShotSteps.CollectDetailsAsync(artifacts);

        Assert.Equal("collect-details", plan.Label);
        Assert.Equal(CollectDetails.Text, plan.Prompt);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.SpecsEpic, OrchestrationArtifactPaths.Spec(1), OrchestrationArtifactPaths.Plan }
                .OrderBy(x => x, StringComparer.Ordinal),
            plan.AllowedReads.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Contains(plan.AllowedReadGlobs, glob => glob.Directory == ".agents/specs" && glob.Pattern == "*.md");
        Assert.Equal([OrchestrationArtifactPaths.Details], plan.AllowedWrites);
        Assert.Empty(plan.AllowedWriteGlobs);
        Assert.Equal([OrchestrationArtifactPaths.Details], plan.RequiredOutputs);
        Assert.Null(plan.RequiredOutputGlob);
        Assert.Null(plan.ChangedGuard);
    }

    [Fact]
    public async Task ExtractMilestonesAsync_BuildsScopedOperationWithPlanChangedGuardAndMilestoneGlob()
    {
        var (artifacts, _, _) = NewArtifacts();

        Cli.ArtifactOperationPlan plan = await Cli.OneShotSteps.ExtractMilestonesAsync(artifacts);

        Assert.Equal("extract-milestones", plan.Label);
        Assert.Equal(ExtractMilestones.Text, plan.Prompt);
        Assert.Equal([OrchestrationArtifactPaths.Plan], plan.AllowedReads);
        Assert.Equal([OrchestrationArtifactPaths.Plan], plan.AllowedWrites);
        Assert.Contains(
            plan.AllowedWriteGlobs,
            glob => glob.Directory == OrchestrationArtifactPaths.MilestonesDirectory
                && glob.Pattern == OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Equal(OrchestrationArtifactPaths.Plan, plan.ChangedGuard);
        Assert.True(plan.RequireChecklistInGlob);
        Assert.Equal(OrchestrationArtifactPaths.MilestonesDirectory, plan.RequiredOutputGlob?.Directory);
    }

    [Fact]
    public async Task ExtractDetailsAsync_BuildsScopedOperationForDetailsAndExistingMilestones()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [ ] a");

        Cli.ArtifactOperationPlan plan = await Cli.OneShotSteps.ExtractDetailsAsync(artifacts);

        Assert.Equal("extract-details", plan.Label);
        Assert.Equal(ExtractDetails.Text, plan.Prompt);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.Details, ".agents/milestones/m1.md" }
                .OrderBy(x => x, StringComparer.Ordinal),
            plan.AllowedReads.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal([OrchestrationArtifactPaths.Details], plan.AllowedWrites);
        Assert.Contains(
            plan.AllowedWriteGlobs,
            glob => glob.Directory == OrchestrationArtifactPaths.MilestonesDirectory
                && glob.Pattern == OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Equal([OrchestrationArtifactPaths.Details], plan.RequiredOutputs);
        Assert.Null(plan.ChangedGuard);
    }
}

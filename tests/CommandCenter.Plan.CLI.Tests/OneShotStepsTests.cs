using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Plan.Cli;
using Xunit;

namespace CommandCenter.Plan.Cli.Tests;

public class OneShotStepsTests
{
    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static (PlanArtifacts Artifacts, MemoryArtifactStore Store, Repository Repo) NewArtifacts()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new PlanArtifacts(store, repo), store, repo);
    }

    private static (SandboxedPromptStep Step, FakeAgentRuntime Rt, FakeSandboxWorkspaceFactory Sandboxes, MemoryArtifactStore Store, PlanArtifacts Artifacts, Repository Repo) NewPipeline()
    {
        var (artifacts, store, repo) = NewArtifacts();
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandboxes = new FakeSandboxWorkspaceFactory();
        return (new SandboxedPromptStep(rt, sandboxes, artifacts, con, repo), rt, sandboxes, store, artifacts, repo);
    }

    [Fact]
    public async Task CollectDetailsAsync_BuildsExpectedPlan_SeedingAllSpecsAndPlan_RequiringAndCopyingBackDetailsOnly()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Spec(1)), "SPEC 1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        SandboxedStepPlan plan = await OneShotSteps.CollectDetailsAsync(artifacts);

        Assert.Equal("collect-details", plan.Label);
        Assert.Equal(CollectDetails.Text, plan.Prompt);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.SpecsRoadmap, OrchestrationArtifactPaths.Spec(1), OrchestrationArtifactPaths.Plan }
                .OrderBy(x => x, StringComparer.Ordinal),
            plan.Seeds.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { OrchestrationArtifactPaths.Details }, plan.RequiredOutputs);
        Assert.Null(plan.RequiredOutputGlob);
        Assert.Null(plan.ChangedGuard);
        Assert.Equal(new[] { OrchestrationArtifactPaths.Details }, plan.CopyBackFiles);
        Assert.Null(plan.CopyBackGlob);
        Assert.False(plan.RequireChecklistInGlob);
    }

    [Fact]
    public async Task CollectDetailsAsync_NoSpecFilesPresent_SeedsJustThePlan()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        SandboxedStepPlan plan = await OneShotSteps.CollectDetailsAsync(artifacts);

        Assert.Equal(new[] { OrchestrationArtifactPaths.Plan }, plan.Seeds);
    }

    [Fact]
    public async Task CollectDetails_EndToEnd_SeedsSpecsAndPlanAtPrefixStrippedPaths_AndCopiesBackDetails()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP CONTENT");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN CONTENT");

        string? seenRoadmap = null;
        string? seenPlan = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(sandboxes.Root, spec.WorkingDirectory);
            Assert.Equal("workspace-write", spec.Sandbox.Identifier);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal(CollectDetails.Text, prompt);
            seenRoadmap = s.ReadAsync(sandboxes.Resolve("specs/roadmap.md")).Result;
            seenPlan = s.ReadAsync(sandboxes.Resolve("plan.md")).Result;
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS CONTENT").Wait();
            return Turns.Completed("collected details");
        }));

        SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);
        await step.RunAsync(collectDetails, CancellationToken.None);

        Assert.Equal("ROADMAP CONTENT", seenRoadmap);
        Assert.Equal("PLAN CONTENT", seenPlan);
        Assert.Equal("DETAILS CONTENT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/plan.md")));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/specs/roadmap.md")));
    }

    [Fact]
    public async Task CollectDetails_TurnFailed_ThrowsWithDiagnosticsTail_AndDetailsNotWritten()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "collect-details stderr tail")));

        SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(collectDetails, CancellationToken.None));

        Assert.Contains("collect-details stderr tail", ex.Message);
        Assert.Contains("Agent stderr (tail):", ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task CollectDetails_CompletedButNoDetailsWritten_Throws_AndDetailsNotWritten()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(collectDetails, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Details, ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task CollectDetails_PlanMissing_ThrowsBeforeAnyCodexCall()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        // .agents/plan.md is never written.

        SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(collectDetails, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Empty(rt.OneShotCalls);
    }
}

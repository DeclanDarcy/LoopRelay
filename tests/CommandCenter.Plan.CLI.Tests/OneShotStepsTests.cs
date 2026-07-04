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
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Spec(1)), "SPEC 1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        SandboxedStepPlan plan = await OneShotSteps.CollectDetailsAsync(artifacts);

        Assert.Equal("collect-details", plan.Label);
        Assert.Equal(CollectDetails.Text, plan.Prompt);
        Assert.Equal(
            new[] { OrchestrationArtifactPaths.SpecsEpic, OrchestrationArtifactPaths.Spec(1), OrchestrationArtifactPaths.Plan }
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
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC CONTENT");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN CONTENT");

        string? seenEpic = null;
        string? seenPlan = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(sandboxes.Root, spec.WorkingDirectory);
            Assert.Equal("workspace-write", spec.Sandbox.Identifier);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal(CollectDetails.Text, prompt);
            seenEpic = s.ReadAsync(sandboxes.Resolve("specs/epic.md")).Result;
            seenPlan = s.ReadAsync(sandboxes.Resolve("plan.md")).Result;
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS CONTENT").Wait();
            return Turns.Completed("collected details");
        }));

        SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);
        await step.RunAsync(collectDetails, CancellationToken.None);

        Assert.Equal("EPIC CONTENT", seenEpic);
        Assert.Equal("PLAN CONTENT", seenPlan);
        Assert.Equal("DETAILS CONTENT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/plan.md")));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/specs/epic.md")));
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

    [Fact]
    public async Task ExtractMilestonesAsync_BuildsExpectedPlan_SeedingOnlyPlan_RequiringGlobAndChangedGuard()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        SandboxedStepPlan plan = await OneShotSteps.ExtractMilestonesAsync(artifacts);

        Assert.Equal("extract-milestones", plan.Label);
        Assert.Equal(ExtractMilestones.Text, plan.Prompt);
        Assert.Equal(new[] { OrchestrationArtifactPaths.Plan }, plan.Seeds);
        Assert.Empty(plan.RequiredOutputs);
        Assert.Equal(
            (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            plan.RequiredOutputGlob);
        Assert.Equal(OrchestrationArtifactPaths.Plan, plan.ChangedGuard);
        Assert.Equal(new[] { OrchestrationArtifactPaths.Plan }, plan.CopyBackFiles);
        Assert.Equal(
            (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            plan.CopyBackGlob);
        Assert.True(plan.RequireChecklistInGlob);
    }

    [Fact]
    public async Task ExtractMilestones_EndToEnd_SeedsPlanOnly_RewritesPlan_AndCopiesBackPlanPlusMilestones()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");

        string? seenPlan = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(sandboxes.Root, spec.WorkingDirectory);
            Assert.Equal("workspace-write", spec.Sandbox.Identifier);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal(ExtractMilestones.Text, prompt);
            seenPlan = s.ReadAsync(sandboxes.Resolve("plan.md")).Result;
            s.WriteAsync(sandboxes.Resolve("plan.md"), "PLAN (See ./milestones/m1-foo.md)").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "- [ ] do the thing").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m2-bar.md"), "- [x] done thing").Wait();
            return Turns.Completed("split into milestones");
        }));

        SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);
        await step.RunAsync(extractMilestones, CancellationToken.None);

        Assert.Equal("PLAN ORIGINAL", seenPlan);
        Assert.Equal(
            "PLAN (See ./milestones/m1-foo.md)",
            await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.Equal(
            "- [ ] do the thing",
            await store.ReadAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md"))));
        Assert.Equal(
            "- [x] done thing",
            await store.ReadAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m2-bar.md"))));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/plan.md")));
    }

    [Fact]
    public async Task ExtractMilestones_PlanUnchanged_ThrowsAndCopiesBackNothing()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "- [ ] do the thing").Wait();
            return Turns.Completed("wrote milestones but did not rewrite plan");
        }));

        SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(extractMilestones, CancellationToken.None));

        Assert.Contains("unchanged", ex.Message);
        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.False(await store.ExistsAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md"))));
    }

    [Fact]
    public async Task ExtractMilestones_MilestonesWithoutAnyStrictCheckbox_ThrowsFalseClosureMessage_AndCopiesBackNothing()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("plan.md"), "PLAN (See ./milestones/m1-foo.md)").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "no checkboxes here").Wait();
            return Turns.Completed("split into milestones without checkboxes");
        }));

        SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(extractMilestones, CancellationToken.None));

        Assert.Equal("extracted milestones contain no trackable checkboxes", ex.Message);
        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task ExtractMilestones_EmptyMilestonesGlob_Throws()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("plan.md"), "PLAN (See ./milestones/m1-foo.md)").Wait();
            return Turns.Completed("rewrote plan but wrote no milestone files");
        }));

        SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);

        await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(extractMilestones, CancellationToken.None));
    }

    [Fact]
    public async Task ExtractDetailsAsync_BuildsExpectedPlan_SeedingDetailsAndAllMilestones_NoChangedGuard()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md")), "- [ ] a");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m2-bar.md")), "- [x] b");

        SandboxedStepPlan plan = await OneShotSteps.ExtractDetailsAsync(artifacts);

        Assert.Equal("extract-details", plan.Label);
        Assert.Equal(ExtractDetails.Text, plan.Prompt);
        Assert.Equal(
            new[]
            {
                OrchestrationArtifactPaths.Details,
                ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md"),
                ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m2-bar.md"),
            }.OrderBy(x => x, StringComparer.Ordinal),
            plan.Seeds.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { OrchestrationArtifactPaths.Details }, plan.RequiredOutputs);
        Assert.Null(plan.RequiredOutputGlob);
        Assert.Null(plan.ChangedGuard);
        Assert.Equal(new[] { OrchestrationArtifactPaths.Details }, plan.CopyBackFiles);
        Assert.Equal(
            (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            plan.CopyBackGlob);
        Assert.False(plan.RequireChecklistInGlob);
    }

    [Fact]
    public async Task ExtractDetailsAsync_NoMilestonesPresent_SeedsJustDetails()
    {
        var (artifacts, store, repo) = NewArtifacts();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");

        SandboxedStepPlan plan = await OneShotSteps.ExtractDetailsAsync(artifacts);

        Assert.Equal(new[] { OrchestrationArtifactPaths.Details }, plan.Seeds);
    }

    [Fact]
    public async Task ExtractDetails_EndToEnd_SeedsDetailsAndMilestones_AndCopiesBackBoth()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS ORIGINAL");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md")), "- [ ] a");

        string? seenDetails = null;
        string? seenMilestone = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(sandboxes.Root, spec.WorkingDirectory);
            Assert.Equal("workspace-write", spec.Sandbox.Identifier);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal(ExtractDetails.Text, prompt);
            seenDetails = s.ReadAsync(sandboxes.Resolve("details.md")).Result;
            seenMilestone = s.ReadAsync(sandboxes.Resolve("milestones/m1-foo.md")).Result;
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS UNIVERSAL ONLY").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "- [ ] a\nmilestone-specific detail").Wait();
            return Turns.Completed("redistributed details");
        }));

        SandboxedStepPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);
        await step.RunAsync(extractDetails, CancellationToken.None);

        Assert.Equal("DETAILS ORIGINAL", seenDetails);
        Assert.Equal("- [ ] a", seenMilestone);
        Assert.Equal("DETAILS UNIVERSAL ONLY", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.Equal(
            "- [ ] a\nmilestone-specific detail",
            await store.ReadAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md"))));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/details.md")));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/milestones/m1-foo.md")));
    }

    [Fact]
    public async Task ExtractDetails_NoOpDetails_UnchangedContent_StillPasses()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS UNIVERSAL ONLY");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            // Agent leaves details.md byte-for-byte unchanged: a legitimate no-op since ExtractDetails
            // declares no changed guard (unlike ExtractMilestones' mandatory plan rewrite).
            return Turns.Completed("no milestone-specific details found; nothing to redistribute");
        }));

        SandboxedStepPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);
        await step.RunAsync(extractDetails, CancellationToken.None); // does not throw

        Assert.Equal("DETAILS UNIVERSAL ONLY", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task ExtractDetails_DetailsMissing_ThrowsBeforeAnyCodexCall()
    {
        var (step, rt, sandboxes, store, artifacts, repo) = NewPipeline();
        // .agents/details.md is never written.

        SandboxedStepPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(extractDetails, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Details, ex.Message);
        Assert.Empty(rt.OneShotCalls);
    }
}

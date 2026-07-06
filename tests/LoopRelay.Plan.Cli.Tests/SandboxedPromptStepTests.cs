using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class SandboxedPromptStepTests
{
    private static (Cli.SandboxedPromptStep Step, FakeAgentRuntime Rt, FakeSandboxWorkspaceFactory Sandboxes, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new Cli.PlanArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandboxes = new FakeSandboxWorkspaceFactory();
        return (new Cli.SandboxedPromptStep(rt, sandboxes, artifacts, con, repo), rt, sandboxes, store, repo, con);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static Cli.SandboxedStepPlan Plan(
        IReadOnlyList<string>? seeds = null,
        IReadOnlyList<string>? requiredOutputs = null,
        (string Directory, string Pattern)? requiredGlob = null,
        string? changedGuard = null,
        IReadOnlyList<string>? copyBackFiles = null,
        (string Directory, string Pattern)? copyBackGlob = null,
        bool requireChecklist = false,
        string prompt = "PROMPT",
        string label = "step") =>
        new(
            label,
            prompt,
            seeds ?? [],
            requiredOutputs ?? [],
            requiredGlob,
            changedGuard,
            copyBackFiles ?? [],
            copyBackGlob,
            requireChecklist);

    [Fact]
    public async Task RunAsync_SeedsDeclaredFiles_AtPrefixStrippedSandboxPaths_NeverUnderSandboxDotAgents()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN CONTENT");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC CONTENT");

        string? seenPlan = null;
        string? seenEpic = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            seenPlan = s.ReadAsync(sandboxes.Resolve("plan.md")).Result;
            seenEpic = s.ReadAsync(sandboxes.Resolve("specs/epic.md")).Result;
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS").Wait();
            return Turns.Completed("done");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.SpecsEpic],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("PLAN CONTENT", seenPlan);
        Assert.Equal("EPIC CONTENT", seenEpic);
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/plan.md")));
        Assert.False(await store.ExistsAsync(sandboxes.Resolve(".agents/specs/epic.md")));
    }

    [Fact]
    public async Task RunAsync_RunsOneShotWithSandboxWorkingDirectory_WorkspaceWrite_XhighEffort_AndDeclaredPrompt()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(sandboxes.Root, spec.WorkingDirectory);
            Assert.Equal("workspace-write", spec.Sandbox.Identifier);
            Assert.True(spec.Sandbox.CanWriteWorkspace);
            Assert.False(spec.Sandbox.CanAccessNetwork);
            Assert.False(spec.Sandbox.RequiresApproval);
            Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
            Assert.Equal("xhigh", spec.Effort.Identifier);
            Assert.Equal("MY PROMPT", prompt);
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS").Wait();
            return Turns.Completed("done");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            prompt: "MY PROMPT",
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        await step.RunAsync(plan, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_HappyPath_CreatesOneSandbox_DisposesIt_AndCopiesRequiredOutputBackToRepo()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("details.md"), "DETAILS CONTENT").Wait();
            return Turns.Completed("wrote details");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("DETAILS CONTENT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.Equal(1, sandboxes.CreatedCount);
        Assert.Single(sandboxes.Disposed);
    }

    [Fact]
    public async Task RunAsync_MissingSeed_ThrowsNamingThePath_BeforeAnyCodexCall_AndDisposesSandbox()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        // Plan.md is never written.
        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Empty(rt.OneShotCalls);
        Assert.Single(sandboxes.Disposed);
    }

    [Fact]
    public async Task RunAsync_TurnFailed_ThrowsWithDiagnosticsTail_AndCopiesBackNothing()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "one-shot stderr tail")));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains("one-shot stderr tail", ex.Message);
        Assert.Contains("Agent stderr (tail):", ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task RunAsync_TurnFailed_AfterAgentWroteValidOutputIntoSandbox_StillCopiesBackNothing()
    {
        // Falsifies the "failed turn copies nothing back" invariant for real: the sandbox holds a perfectly
        // valid output that WOULD satisfy every gate — the turn failure alone must keep it out of the repo.
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("details.md"), "VALID DETAILS WRITTEN BEFORE FAILURE").Wait();
            return Turns.Failed("boom", "stderr tail here");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains("stderr tail here", ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task RunAsync_TurnFailedWithoutDiagnostics_ThrowsWithoutDiagnosticsSuffix()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom")));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.DoesNotContain("Agent stderr (tail):", ex.Message);
    }

    [Fact]
    public async Task RunAsync_CompletedButRequiredOutputMissing_Throws_AndCopiesBackNothing()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredOutputs: [OrchestrationArtifactPaths.Details],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Details, ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }

    [Fact]
    public async Task RunAsync_RequiredOutputGlobEmpty_Throws()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("wrote nothing under milestones")));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern));

        await Assert.ThrowsAsync<Cli.PlanStepException>(() => step.RunAsync(plan, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_RequiredOutputGlobNonEmpty_CopyBackGlob_CopiesEachMatch_FlatMapped()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "- [ ] a").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m2-bar.md"), "- [x] b").Wait();
            return Turns.Completed("wrote milestones");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            copyBackGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern));

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal(
            "- [ ] a",
            await store.ReadAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1-foo.md"))));
        Assert.Equal(
            "- [x] b",
            await store.ReadAsync(Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m2-bar.md"))));
    }

    [Fact]
    public async Task RunAsync_RequireChecklistInGlob_NoCheckboxesAcrossAnyMatch_ThrowsFalseClosureMessage()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "no checkboxes here").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m2-bar.md"), "* [ ] wrong bullet style").Wait();
            return Turns.Completed("wrote milestones");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            requireChecklist: true);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Equal("extracted milestones contain no trackable checkboxes", ex.Message);
    }

    [Fact]
    public async Task RunAsync_RequireChecklistInGlob_AtLeastOneCheckboxAcrossMatches_Passes()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("milestones/m1-foo.md"), "no checkboxes here").Wait();
            s.WriteAsync(sandboxes.Resolve("milestones/m2-bar.md"), "- [ ] one trackable item").Wait();
            return Turns.Completed("wrote milestones");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            requiredGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            requireChecklist: true);

        await step.RunAsync(plan, CancellationToken.None); // does not throw
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_UnchangedContent_Throws_AndRepoCopyIsUntouched()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did not rewrite plan")));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Contains("unchanged", ex.Message);
        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_DeletedInSandbox_Throws()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.DeleteAsync(sandboxes.Resolve("plan.md")).Wait();
            return Turns.Completed("deleted the plan");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_ContentDiffers_PassesAndCopiesBackStrictly()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("plan.md"), "PLAN REWRITTEN").Wait();
            return Turns.Completed("rewrote plan");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("PLAN REWRITTEN", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_ChangedGuardNotAmongSeeds_ThrowsMisconfiguration_BeforeAnyCodexCall()
    {
        // A ChangedGuard with no seeded snapshot would silently degrade to a "differs from empty" check —
        // the misconfiguration must fail loud, and before burning a codex turn.
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Details],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        Cli.PlanStepException ex = await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => step.RunAsync(plan, CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Contains("Seeds", ex.Message);
        Assert.Empty(rt.OneShotCalls);
        Assert.Single(sandboxes.Disposed);
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_CaseOnlyDifference_PassesTheOrdinalGate_AndCopiesBack()
    {
        // The gate compares ORDINAL: a case-only rewrite IS a change and must pass (and copy back).
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("plan.md"), "plan original").Wait();
            return Turns.Completed("changed only the casing");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("plan original", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_TrailingWhitespaceOnlyDifference_PassesTheOrdinalGate_AndCopiesBack()
    {
        // Ordinal means no trimming/normalization: a trailing-newline-only rewrite IS a change and must pass.
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandboxes.Resolve("plan.md"), "PLAN ORIGINAL\n").Wait();
            return Turns.Completed("appended a newline");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Plan],
            changedGuard: OrchestrationArtifactPaths.Plan,
            copyBackFiles: [OrchestrationArtifactPaths.Plan]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("PLAN ORIGINAL\n", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_PathOutsideDotAgents_MapsToItselfInBothDirections_DocumentingStripAgentsPrefixPassThrough()
    {
        // StripAgentsPrefix only strips a leading ".agents/"; any other repo-relative path passes through
        // unchanged (no such path exists in the pipeline today — this pins the current pass-through behavior).
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, "notes.md"), "NOTES ORIGINAL");

        string? seenInSandbox = null;
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            seenInSandbox = s.ReadAsync(sandboxes.Resolve("notes.md")).Result;
            s.WriteAsync(sandboxes.Resolve("notes.md"), "NOTES REWRITTEN").Wait();
            return Turns.Completed("rewrote notes");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: ["notes.md"],
            requiredOutputs: ["notes.md"],
            copyBackFiles: ["notes.md"]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("NOTES ORIGINAL", seenInSandbox); // seeded at the unmapped sandbox path
        Assert.Equal("NOTES REWRITTEN", await store.ReadAsync(Resolve(repo, "notes.md"))); // copied back to the same repo path
    }

    [Fact]
    public async Task RunAsync_CopyBackFiles_ExistenceGuarded_SkipsAbsentNonGuardedFile_LeavingRepoUntouched()
    {
        var (step, rt, sandboxes, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "OLD DETAILS");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            // Agent deletes details.md in its sandbox rather than leaving it — a legitimate no-op deletion.
            s.DeleteAsync(sandboxes.Resolve("details.md")).Wait();
            return Turns.Completed("did not touch details");
        }));

        Cli.SandboxedStepPlan plan = Plan(
            seeds: [OrchestrationArtifactPaths.Details, OrchestrationArtifactPaths.Plan],
            copyBackFiles: [OrchestrationArtifactPaths.Details]);

        await step.RunAsync(plan, CancellationToken.None);

        Assert.Equal("OLD DETAILS", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
    }
}

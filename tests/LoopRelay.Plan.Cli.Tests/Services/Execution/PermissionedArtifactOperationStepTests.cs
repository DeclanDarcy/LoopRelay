using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Execution;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using LoopRelay.Plan.Cli.Tests.Models;
using LoopRelay.Plan.Cli.Tests.Services.Agents;
using LoopRelay.Plan.Cli.Tests.Services.Support;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class PermissionedArtifactOperationStepTests
{
    private static (PermissionedArtifactOperationStep Step, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo)
        New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new PermissionedArtifactOperationStep(rt, artifacts, con, repo), rt, store, repo);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static ArtifactOperationPlan Plan(
        IReadOnlyList<string>? allowedReads = null,
        IReadOnlyList<OperationPathGlob>? allowedReadGlobs = null,
        IReadOnlyList<string>? allowedWrites = null,
        IReadOnlyList<OperationPathGlob>? allowedWriteGlobs = null,
        IReadOnlyList<string>? requiredOutputs = null,
        OperationPathGlob? requiredGlob = null,
        string? changedGuard = null,
        bool requireChecklist = false,
        string prompt = "PROMPT",
        string label = "step") =>
        new(
            label,
            prompt,
            allowedReads ?? [],
            allowedReadGlobs ?? [],
            allowedWrites ?? [],
            allowedWriteGlobs ?? [],
            requiredOutputs ?? [],
            requiredGlob,
            changedGuard,
            requireChecklist);

    [Fact]
    public async Task RunAsync_OpensFreshReadOnlyApprovalScopedSession_AndUsesRepositoryPaths()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Equal(repo.Path, spec.WorkingDirectory);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.True(spec.Sandbox.CanWriteWorkspace);
            Assert.True(spec.Sandbox.CanAccessNetwork);
            Assert.False(spec.Sandbox.RequiresApproval);
            Assert.Equal(AgentEffort.XHigh, spec.Effort);
            Assert.NotNull(spec.OperationPermissionProfile);
            Assert.Equal("MY PROMPT", prompt);
            Assert.Equal("PLAN", s.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)).Result);
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS").Wait();
            return Turns.Completed("done");
        }));

        await step.RunAsync(Plan(
            prompt: "MY PROMPT",
            allowedReads: [OrchestrationArtifactPaths.Plan],
            allowedWrites: [OrchestrationArtifactPaths.Details],
            requiredOutputs: [OrchestrationArtifactPaths.Details]), CancellationToken.None);

        Assert.Equal("DETAILS", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Details)));
        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(1, rt.ClosedSessions);
        Assert.Empty(rt.OneShotCalls);
    }

    [Fact]
    public async Task RunAsync_MissingRequiredRead_ThrowsBeforeOpeningSession()
    {
        var (step, rt, _, _) = New();

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(Plan(allowedReads: [OrchestrationArtifactPaths.Plan]), CancellationToken.None));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Equal(0, rt.OpenSessions);
    }

    [Fact]
    public async Task RunAsync_FailedTurn_RestoresExistingWritesAndRemovesNewGlobFiles()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN MUTATED").Wait();
            s.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [ ] leaked").Wait();
            return Turns.Failed("boom", "stderr tail");
        }));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(Plan(
                allowedReads: [OrchestrationArtifactPaths.Plan],
                allowedWrites: [OrchestrationArtifactPaths.Plan],
                allowedWriteGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)]),
                CancellationToken.None));

        Assert.Contains("stderr tail", ex.Message);
        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.False(await store.ExistsAsync(Resolve(repo, ".agents/milestones/m1.md")));
    }

    [Fact]
    public async Task RunAsync_FailedGate_RestoresCandidateWrites()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN MUTATED").Wait();
            return Turns.Completed("done");
        }));

        await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(Plan(
                allowedReads: [OrchestrationArtifactPaths.Plan],
                allowedWrites: [OrchestrationArtifactPaths.Plan],
                requiredOutputs: [OrchestrationArtifactPaths.Details]),
                CancellationToken.None));

        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_ChangedGuard_RequiresOrdinalChange()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(Plan(
                allowedReads: [OrchestrationArtifactPaths.Plan],
                allowedWrites: [OrchestrationArtifactPaths.Plan],
                changedGuard: OrchestrationArtifactPaths.Plan),
                CancellationToken.None));

        Assert.Contains("unchanged", ex.Message);
        Assert.Equal("PLAN ORIGINAL", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
    }

    [Fact]
    public async Task RunAsync_GlobGate_RequiresStrictChecklist_WhenConfigured()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "no checkboxes").Wait();
            return Turns.Completed("done");
        }));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync(Plan(
                allowedReads: [OrchestrationArtifactPaths.Plan],
                allowedWriteGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
                requiredGlob: new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
                requireChecklist: true),
                CancellationToken.None));

        Assert.Equal("extracted milestones contain no trackable checkboxes", ex.Message);
        Assert.False(await store.ExistsAsync(Resolve(repo, ".agents/milestones/m1.md")));
    }

    [Fact]
    public async Task RunAsync_GlobWritesRemainOnSuccess()
    {
        var (step, rt, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN ORIGINAL");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN (See ./milestones/m1.md)").Wait();
            s.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [ ] task").Wait();
            return Turns.Completed("done");
        }));

        await step.RunAsync(Plan(
            allowedReads: [OrchestrationArtifactPaths.Plan],
            allowedWrites: [OrchestrationArtifactPaths.Plan],
            allowedWriteGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            requiredGlob: new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            changedGuard: OrchestrationArtifactPaths.Plan,
            requireChecklist: true), CancellationToken.None);

        Assert.Equal("PLAN (See ./milestones/m1.md)", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Plan)));
        Assert.Equal("- [ ] task", await store.ReadAsync(Resolve(repo, ".agents/milestones/m1.md")));
    }
}

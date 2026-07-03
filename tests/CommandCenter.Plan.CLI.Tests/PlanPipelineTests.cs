using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Plan.Cli;
using Xunit;

namespace CommandCenter.Plan.Cli.Tests;

public class PlanPipelineTests
{
    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static string MilestonePath(string fileName) =>
        ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, fileName);

    private sealed record Harness(
        PlanPipeline Pipeline,
        FakeAgentRuntime Runtime,
        FakeSandboxWorkspaceFactory Sandboxes,
        MemoryArtifactStore Store,
        Repository Repo,
        RecordingLoopConsole Console);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var console = new RecordingLoopConsole();
        var runtime = new FakeAgentRuntime(store);
        var sandboxes = new FakeSandboxWorkspaceFactory();

        var preflight = new PreflightGate(artifacts);
        var planSession = new PlanSession(runtime, artifacts, console, repo);
        var review = new ReviewStep(runtime, artifacts, console, repo);
        var oneShot = new SandboxedPromptStep(runtime, sandboxes, artifacts, console, repo);
        var pipeline = new PlanPipeline(preflight, planSession, review, oneShot, artifacts, console);

        return new Harness(pipeline, runtime, sandboxes, store, repo, console);
    }

    private static IReadOnlyList<string> PhaseSequence(RecordingLoopConsole console) =>
        console.Events.Where(e => e.Kind == "phase").Select(e => e.Text).ToList();

    [Fact]
    public async Task RunAsync_HappyPath_RunsAllStepsInOrder_AndProducesTheExpectedFinalArtifactState()
    {
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");

        string? seenPlanAtReview = null;
        string? reviewOutput = null;
        string? seenFeedbackAtRevise = null;

        // SessionTurns is ONE shared queue dequeued in invocation order across both sessions: WritePlan,
        // then Review (a fresh session opened between the two planning turns), then Revise (back on the
        // same warm planning session).
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(WritePlan.Text, prompt);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V1").Wait();
            return Turns.Completed("wrote plan");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            seenPlanAtReview = AdversarialPlanReview.Render("PLAN V1");
            Assert.Equal(seenPlanAtReview, prompt);
            reviewOutput = "- CONDITIONAL PASS\nTighten milestone 2.";
            return Turns.Completed(reviewOutput);
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            seenFeedbackAtRevise = RevisePlan.Render(reviewOutput);
            Assert.Equal(seenFeedbackAtRevise, prompt);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V2 REVISED").Wait();
            return Turns.Completed("revised plan");
        }));

        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(CollectDetails.Text, prompt);
            s.WriteAsync(h.Sandboxes.Resolve("details.md"), "DETAILS V1").Wait();
            return Turns.Completed("collected details");
        }));
        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(ExtractMilestones.Text, prompt);
            Assert.Equal("PLAN V2 REVISED", s.ReadAsync(h.Sandboxes.Resolve("plan.md")).Result);
            s.WriteAsync(h.Sandboxes.Resolve("plan.md"), "PLAN V2 (See ./milestones/m1-thing.md)").Wait();
            s.WriteAsync(h.Sandboxes.Resolve("milestones/m1-thing.md"), "- [ ] do the thing").Wait();
            return Turns.Completed("split into milestones");
        }));
        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(ExtractDetails.Text, prompt);
            s.WriteAsync(h.Sandboxes.Resolve("details.md"), "DETAILS FINAL").Wait();
            return Turns.Completed("redistributed details");
        }));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Completed, outcome);

        // Final artifact state.
        Assert.Equal(
            "PLAN V2 (See ./milestones/m1-thing.md)",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)));
        Assert.Equal("DETAILS FINAL", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details)));
        string? milestone = await h.Store.ReadAsync(Resolve(h.Repo, MilestonePath("m1-thing.md")));
        Assert.Equal("- [ ] do the thing", milestone);
        (int total, _) = MilestoneChecklist.CountCheckboxes(milestone!);
        Assert.True(total >= 1);

        // Phase sequence.
        Assert.Equal(
            new[]
            {
                "Preflight", "Write Plan", "Adversarial Review", "Revise Plan",
                "Collect Details", "Extract Milestones", "Extract Details",
            },
            PhaseSequence(h.Console));

        // Session accounting: WritePlan/Revise share one session, Review opens its own.
        Assert.Equal(2, h.Runtime.OpenSessions);
        Assert.Equal(2, h.Runtime.ClosedSessions);

        // One sandbox per one-shot; the fake's single shared Root means cross-step isolation is not
        // certified here (the per-step m4/m5 tests carry that).
        Assert.Equal(3, h.Sandboxes.CreatedCount);
        Assert.Equal(3, h.Sandboxes.Disposed.Count);

        // Cross-checks that the closures above actually ran and asserted what this test claims.
        Assert.NotNull(seenPlanAtReview);
        Assert.NotNull(seenFeedbackAtRevise);

        // Double-close safety: DisposeAsync after the pipeline's own eager close must not close again.
        await h.Pipeline.DisposeAsync();
        Assert.Equal(2, h.Runtime.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_PreflightBlocked_MakesZeroCodexCalls()
    {
        Harness h = New();
        // .agents/specs/roadmap.md is never written -> preflight reports a violation.

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, h.Runtime.OpenSessions);
        Assert.Empty(h.Runtime.OneShotCalls);
        Assert.Contains(h.Console.Events, e => e.Kind == "error");
        Assert.Equal(new[] { "Preflight" }, PhaseSequence(h.Console));
    }

    [Fact]
    public async Task RunAsync_MidPipelineFailure_CollectDetailsFailed_ReturnsFailed_ClosesAllSessions_KeepsRevisedPlan()
    {
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V1").Wait();
            return Turns.Completed("wrote plan");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("- PASS")));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V2 REVISED").Wait();
            return Turns.Completed("revised plan");
        }));

        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Failed("boom", "collect-details stderr tail")));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Failed, outcome);
        Assert.Equal(2, h.Runtime.ClosedSessions);
        Assert.Equal(
            "PLAN V2 REVISED",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains("collect-details stderr tail"));
    }

    [Fact]
    public async Task RunAsync_CancelledMidRun_ReturnsCancelled_AndDisposeAsyncStillClosesTheOpenPlanningSession()
    {
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");

        using var cts = new CancellationTokenSource();
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V1").Wait();
            return Turns.Completed("wrote plan");
        }));
        // Simulates cancellation arriving mid-run, during the adversarial review turn: the CALLER's token is
        // cancelled (Cancelled is returned only for caller cancellation — see the Failed-path tests below).
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        PlanOutcome outcome = await h.Pipeline.RunAsync(cts.Token);

        Assert.Equal(PlanOutcome.Cancelled, outcome);
        // The review session closes itself (try/finally) even on the cancellation path; the planning
        // session opened by WritePlanAsync is still open at this point (RevisePlan/eager-close never ran).
        Assert.Equal(1, h.Runtime.ClosedSessions);

        // Mirrors Program.cs's `finally { await pipeline.DisposeAsync(); }`, which must run on every path.
        await h.Pipeline.DisposeAsync();
        Assert.Equal(2, h.Runtime.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_UnexpectedException_ReturnsFailed_AndSurfacesTheMessageAsAConsoleError()
    {
        // Non-PlanStepException failures (Win32Exception on a bad CODEX_EXECUTABLE, IOException on app-server
        // handshake death, ...) must honor the exit-code contract: message on the console, outcome Failed —
        // never an unhandled crash with a stack trace.
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            throw new InvalidOperationException("codex launcher exploded")));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Failed, outcome);
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains("codex launcher exploded"));
    }

    [Fact]
    public async Task RunAsync_OperationCanceledWithoutCallerCancellation_ReturnsFailed_NotCancelled()
    {
        // An OperationCanceledException while the CALLER's token is not cancelled is not a cancellation the
        // operator asked for (e.g. an internal timeout) — it is a Failed run, and its message is surfaced.
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsRoadmap), "ROADMAP");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            throw new OperationCanceledException("internal turn timeout")));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Failed, outcome);
        Assert.Contains(h.Console.Events, e => e.Kind == "error" && e.Text.Contains("internal turn timeout"));
    }
}

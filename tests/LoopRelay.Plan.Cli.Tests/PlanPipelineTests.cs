using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Projections;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class PlanPipelineTests
{
    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static string MilestonePath(string fileName) =>
        ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, fileName);

    private sealed record Harness(
        Cli.PlanPipeline Pipeline,
        FakeAgentRuntime Runtime,
        FakeSandboxWorkspaceFactory Sandboxes,
        MemoryArtifactStore Store,
        Repository Repo,
        RecordingLoopConsole Console,
        FakeProcessRunner Processes,
        FakeProjectionService Projection);

    private static Harness New(FakeProcessRunner? processes = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new Cli.PlanArtifacts(store, repo);
        var console = new RecordingLoopConsole();
        var runtime = new FakeAgentRuntime(store);
        var sandboxes = new FakeSandboxWorkspaceFactory();
        var git = processes ?? new FakeProcessRunner();
        var projection = new FakeProjectionService("ADVERSARIAL REVIEW PROJECTION");

        var preflight = new Cli.PreflightGate(artifacts);
        var planSession = new Cli.PlanSession(runtime, artifacts, console, repo);
        var review = new Cli.ReviewStep(runtime, artifacts, console, repo);
        var artifactOperation = new Cli.PermissionedArtifactOperationStep(runtime, store, artifacts, console, repo);
        var publisher = new Cli.AgentsSubmodulePublisher(git, repo, console);
        var pipeline = new Cli.PlanPipeline(
            preflight, planSession, review, projection, artifactOperation, publisher, artifacts, console);

        return new Harness(pipeline, runtime, sandboxes, store, repo, console, git, projection);
    }

    private static IReadOnlyList<string> PhaseSequence(RecordingLoopConsole console) =>
        console.Events.Where(e => e.Kind == "phase").Select(e => e.Text).ToList();

    private static bool IsSubmodule(string workingDirectory) =>
        workingDirectory.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);

    /// <summary>Scripts an always-dirty submodule so every publish makes a real commit.</summary>
    private static FakeProcessRunner DirtyGit() => new()
    {
        Handler = (_, args) => args[0] switch
        {
            "status" => FakeProcessRunner.Ok(" M plan.md"),
            "branch" => FakeProcessRunner.Ok("main"),
            _ => FakeProcessRunner.Ok()
        }
    };

    [Fact]
    public async Task RunAsync_HappyPath_RunsAllStepsInOrder_AndProducesTheExpectedFinalArtifactState()
    {
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

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
            seenPlanAtReview = AdversarialPlanReview.Render("ADVERSARIAL REVIEW PROJECTION", "PLAN V1");
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

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(CollectDetails.Text, prompt);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS V1").Wait();
            return Turns.Completed("collected details");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(ExtractMilestones.Text, prompt);
            Assert.Equal("PLAN V2 REVISED", s.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)).Result);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V2 (See ./milestones/m1-thing.md)").Wait();
            s.WriteAsync(Resolve(h.Repo, MilestonePath("m1-thing.md")), "- [ ] do the thing").Wait();
            return Turns.Completed("split into milestones");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, s) =>
        {
            Assert.Equal(ExtractDetails.Text, prompt);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS FINAL").Wait();
            return Turns.Completed("redistributed details");
        }));

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Completed, outcome);

        // Final artifact state.
        Assert.Equal(
            "PLAN V2 (See ./milestones/m1-thing.md)",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)));
        Assert.Equal("DETAILS FINAL", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details)));
        string? milestone = await h.Store.ReadAsync(Resolve(h.Repo, MilestonePath("m1-thing.md")));
        Assert.Equal("- [ ] do the thing", milestone);
        (int total, _) = Cli.MilestoneChecklist.CountCheckboxes(milestone!);
        Assert.True(total >= 1);

        // operational_context.md was seeded from the revised plan right after Revise Plan — before the extract
        // steps rewrote plan.md — so it carries the revised text, not the pointer-rewritten one.
        Assert.Equal(
            "PLAN V2 REVISED",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)));

        // Phase sequence.
        Assert.Equal(
            new[]
            {
                "Preflight", "Write Plan", "Generate Adversarial Review Projection", "Adversarial Review", "Revise Plan",
                "Collect Details", "Extract Milestones", "Extract Details",
            },
            PhaseSequence(h.Console));
        Assert.Equal(1, h.Projection.EnsureFreshCalls);
        Assert.Contains(ProjectionRuntimePromptNames.AdversarialPlanReview, h.Projection.RuntimePromptNames);

        // Session accounting: WritePlan/Revise share one session, Review opens its own, and each scoped artifact
        // operation opens a fresh app-server session.
        Assert.Equal(5, h.Runtime.OpenSessions);
        Assert.Equal(5, h.Runtime.ClosedSessions);

        Assert.Equal(0, h.Sandboxes.CreatedCount);
        Assert.Empty(h.Runtime.OneShotCalls);

        // Cross-checks that the closures above actually ran and asserted what this test claims.
        Assert.NotNull(seenPlanAtReview);
        Assert.NotNull(seenFeedbackAtRevise);

        // Double-close safety: DisposeAsync after the pipeline's own eager close must not close again.
        await h.Pipeline.DisposeAsync();
        Assert.Equal(5, h.Runtime.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_PreflightBlocked_MakesZeroCodexCalls()
    {
        Harness h = New();
        // .agents/specs/epic.md is never written -> preflight reports a violation.

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, h.Runtime.OpenSessions);
        Assert.Empty(h.Runtime.OneShotCalls);
        Assert.Contains(h.Console.Events, e => e.Kind == "error");
        Assert.Equal(new[] { "Preflight" }, PhaseSequence(h.Console));
    }

    [Fact]
    public async Task RunAsync_MidPipelineFailure_CollectDetailsFailed_ReturnsFailed_ClosesAllSessions_KeepsRevisedPlan()
    {
        Harness h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

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

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Failed("boom", "collect-details stderr tail")));

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Failed, outcome);
        Assert.Equal(3, h.Runtime.ClosedSessions);
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
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

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

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(cts.Token);

        Assert.Equal(Cli.PlanOutcome.Cancelled, outcome);
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
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            throw new InvalidOperationException("codex launcher exploded")));

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Failed, outcome);
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
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            throw new OperationCanceledException("internal turn timeout")));

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Failed, outcome);
        Assert.Contains(h.Console.Events, e => e.Kind == "error" && e.Text.Contains("internal turn timeout"));
    }

    /// <summary>Scripts the standard successful codex turns: WritePlan, Review, Revise + the three one-shots.
    /// The Collect Details turn snapshots operational_context.md so its seed-before-collect timing is provable.</summary>
    private static void ScriptFullHappyRun(Harness h, Action<string?>? onCollectDetailsContext = null)
    {
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

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            onCollectDetailsContext?.Invoke(
                s.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)).Result);
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS").Wait();
            return Turns.Completed("collected details");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN V2 (See ./milestones/m1.md)").Wait();
            s.WriteAsync(Resolve(h.Repo, MilestonePath("m1.md")), "- [ ] do the thing").Wait();
            return Turns.Completed("split into milestones");
        }));
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS FINAL").Wait();
            return Turns.Completed("redistributed details");
        }));
    }

    [Fact]
    public async Task RunAsync_FullRun_PublishesSubmoduleAfterEachMutatingStep_AndParentGitlinkExactlyOnceLast()
    {
        FakeProcessRunner git = DirtyGit();
        Harness h = New(git);
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

        string? contextAtCollectDetails = null;
        ScriptFullHappyRun(h, ctx => contextAtCollectDetails = ctx);

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Completed, outcome);

        // operational_context.md was seeded (from the revised plan) BEFORE Collect Details ran, and equals
        // plan.md's content as of the revise step.
        Assert.Equal("PLAN V2 REVISED", contextAtCollectDetails);
        Assert.Equal(
            "PLAN V2 REVISED",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)));

        // The exact ordered submodule commit cadence: one commit per artifact-mutating step, and NONE between
        // Write Plan's and Revise Plan's — the adversarial review step writes nothing and publishes nothing.
        var submoduleCommits = git.Calls
            .Where(c => c.FileName == "git" && IsSubmodule(c.WorkingDirectory) && c.Args[0] == "commit")
            .Select(c => c.Args[2])
            .ToList();
        Assert.Equal(
            new[]
            {
                Cli.AgentsSubmodulePublisher.WritePlanMessage,
                Cli.AgentsSubmodulePublisher.GenerateAdversarialReviewProjectionMessage,
                Cli.AgentsSubmodulePublisher.RevisePlanMessage,
                Cli.AgentsSubmodulePublisher.CollectDetailsMessage,
                Cli.AgentsSubmodulePublisher.ExtractMilestonesMessage,
                Cli.AgentsSubmodulePublisher.ExtractDetailsMessage,
            },
            submoduleCommits);

        // Exactly ONE parent-repo commit — the gitlink pointer — and it is the LAST commit of the entire run.
        var parentCommit = Assert.Single(
            git.Calls, c => c.FileName == "git" && !IsSubmodule(c.WorkingDirectory) && c.Args[0] == "commit");
        Assert.Equal(new[] { "commit", "-m", Cli.AgentsSubmodulePublisher.GitlinkPointerMessage }, parentCommit.Args);
        var allCommits = git.Calls.Where(c => c.FileName == "git" && c.Args[0] == "commit").ToList();
        Assert.Equal(7, allCommits.Count);
        Assert.Equal(Cli.AgentsSubmodulePublisher.GitlinkPointerMessage, allCommits[^1].Args[2]);
    }

    private static async Task SeedCompletePreviousWorkspaceAsync(Harness h)
    {
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "OLD PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "OLD DETAILS");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OLD CTX");
        await h.Store.WriteAsync(Resolve(h.Repo, MilestonePath("m1.md")), "- [x] done");
    }

    [Fact]
    public async Task RunAsync_CompletePreviousWorkspace_BlocksAtPreflight_AndRunsNoProcess()
    {
        Harness h = New();
        // A complete previous-epic workspace is no longer auto-archived by the planning pipeline.
        await SeedCompletePreviousWorkspaceAsync(h);
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "NEXT EPIC");

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(new[] { "Preflight" }, PhaseSequence(h.Console));
        Assert.Empty(h.Processes.Calls);
        Assert.Equal(0, h.Runtime.OpenSessions);
        Assert.Empty(h.Runtime.OneShotCalls);
        Assert.Equal(
            "NEXT EPIC",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic)));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.Plan) && e.Text.Contains("already exists"));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.Details) && e.Text.Contains("already exists"));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.OperationalContext) && e.Text.Contains("already exists"));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.MilestonesDirectory) && e.Text.Contains("not empty"));
    }

    [Fact]
    public async Task RunAsync_CompletePreviousWorkspace_WithoutEpic_ReportsAllPreflightViolations()
    {
        Harness h = New();
        await SeedCompletePreviousWorkspaceAsync(h);

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(new[] { "Preflight" }, PhaseSequence(h.Console));
        Assert.Empty(h.Processes.Calls);
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.SpecsEpic) && e.Text.Contains("not found"));
        Assert.Contains(h.Console.Events, e => e.Kind == "error" && e.Text.Contains("already exists"));
    }

    [Fact]
    public async Task RunAsync_IncompletePreviousWorkspace_PreflightReportsTheExistingViolations()
    {
        Harness h = New();
        // plan.md only — existing planning artifacts are operator-owned cleanup.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "STRAY PLAN");

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.PreflightBlocked, outcome);
        // Nothing is published before a blocked preflight, so no process runs at all.
        Assert.Empty(h.Processes.Calls);
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.Plan) && e.Text.Contains("already exists"));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.SpecsEpic) && e.Text.Contains("not found"));
    }

    [Fact]
    public async Task RunAsync_MidPipelineFailure_PublishesStepsUpToTheFailure_ButNeverTheParentGitlink()
    {
        FakeProcessRunner git = DirtyGit();
        Harness h = New(git);
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

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
        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "stderr tail")));

        Cli.PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(Cli.PlanOutcome.Failed, outcome);

        // The steps that completed before the failure published their submodule commits...
        var submoduleCommits = git.Calls
            .Where(c => c.FileName == "git" && IsSubmodule(c.WorkingDirectory) && c.Args[0] == "commit")
            .Select(c => c.Args[2])
            .ToList();
        Assert.Equal(
            new[]
            {
                Cli.AgentsSubmodulePublisher.WritePlanMessage,
                Cli.AgentsSubmodulePublisher.GenerateAdversarialReviewProjectionMessage,
                Cli.AgentsSubmodulePublisher.RevisePlanMessage,
            },
            submoduleCommits);

        // ...but the parent gitlink pointer is recorded only at the END of a successful run: a failed run
        // makes NO parent-repo git call whatsoever.
        Assert.DoesNotContain(git.Calls, c => !IsSubmodule(c.WorkingDirectory));
    }

}

using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Models;
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
        RecordingLoopConsole Console,
        FakeProcessRunner Processes,
        FakeDecisionSessionResumeStore Resume);

    private static Harness New(FakeProcessRunner? processes = null)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var console = new RecordingLoopConsole();
        var runtime = new FakeAgentRuntime(store);
        var sandboxes = new FakeSandboxWorkspaceFactory();
        var git = processes ?? new FakeProcessRunner();
        var resume = new FakeDecisionSessionResumeStore();

        var rollover = new EpicRolloverStep(git, artifacts, console, repo);
        var preflight = new PreflightGate(artifacts);
        var planSession = new PlanSession(runtime, artifacts, console, repo);
        var review = new ReviewStep(runtime, artifacts, console, repo);
        var oneShot = new SandboxedPromptStep(runtime, sandboxes, artifacts, console, repo);
        var publisher = new AgentsSubmodulePublisher(git, repo, console);
        var pipeline = new PlanPipeline(
            rollover, preflight, planSession, review, oneShot, publisher, artifacts, resume, console);

        return new Harness(pipeline, runtime, sandboxes, store, repo, console, git, resume);
    }

    private static IReadOnlyList<string> PhaseSequence(RecordingLoopConsole console) =>
        console.Events.Where(e => e.Kind == "phase").Select(e => e.Text).ToList();

    private static bool IsSubmodule(string workingDirectory) =>
        workingDirectory.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);

    /// <summary>Scripts an always-dirty submodule so every publish makes a real commit. Guarded on Args being
    /// non-empty (the new-epic invocation under a NEW_EPIC_EXECUTABLE override carries no arguments).</summary>
    private static FakeProcessRunner DirtyGit() => new()
    {
        Handler = (_, args) => args.Count == 0 ? FakeProcessRunner.Ok() : args[0] switch
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

        // operational_context.md was seeded from the revised plan right after Revise Plan — before the extract
        // steps rewrote plan.md — so it carries the revised text, not the pointer-rewritten one.
        Assert.Equal(
            "PLAN V2 REVISED",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)));

        // Phase sequence.
        Assert.Equal(
            new[]
            {
                "Epic Rollover", "Preflight", "Write Plan", "Adversarial Review", "Revise Plan",
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
        // .agents/specs/epic.md is never written -> preflight reports a violation.

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, h.Runtime.OpenSessions);
        Assert.Empty(h.Runtime.OneShotCalls);
        Assert.Contains(h.Console.Events, e => e.Kind == "error");
        Assert.Equal(new[] { "Epic Rollover", "Preflight" }, PhaseSequence(h.Console));
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
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

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
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

        h.Runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            throw new OperationCanceledException("internal turn timeout")));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Failed, outcome);
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

        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            onCollectDetailsContext?.Invoke(
                s.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)).Result);
            s.WriteAsync(h.Sandboxes.Resolve("details.md"), "DETAILS").Wait();
            return Turns.Completed("collected details");
        }));
        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(h.Sandboxes.Resolve("plan.md"), "PLAN V2 (See ./milestones/m1.md)").Wait();
            s.WriteAsync(h.Sandboxes.Resolve("milestones/m1.md"), "- [ ] do the thing").Wait();
            return Turns.Completed("split into milestones");
        }));
        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(h.Sandboxes.Resolve("details.md"), "DETAILS FINAL").Wait();
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

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Completed, outcome);

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
                AgentsSubmodulePublisher.WritePlanMessage,
                AgentsSubmodulePublisher.RevisePlanMessage,
                AgentsSubmodulePublisher.CollectDetailsMessage,
                AgentsSubmodulePublisher.ExtractMilestonesMessage,
                AgentsSubmodulePublisher.ExtractDetailsMessage,
            },
            submoduleCommits);

        // Exactly ONE parent-repo commit — the gitlink pointer — and it is the LAST commit of the entire run.
        var parentCommit = Assert.Single(
            git.Calls, c => c.FileName == "git" && !IsSubmodule(c.WorkingDirectory) && c.Args[0] == "commit");
        Assert.Equal(new[] { "commit", "-m", AgentsSubmodulePublisher.GitlinkPointerMessage }, parentCommit.Args);
        var allCommits = git.Calls.Where(c => c.FileName == "git" && c.Args[0] == "commit").ToList();
        Assert.Equal(6, allCommits.Count);
        Assert.Equal(AgentsSubmodulePublisher.GitlinkPointerMessage, allCommits[^1].Args[2]);
    }

    /// <summary>Wires the fake so the only non-git invocation (new-epic) archives the previous workspace:
    /// plan.md, details.md, operational_context.md and the milestone file are removed. specs/ is NOT touched —
    /// new-epic leaves it in place (whether or not a epic exists there).</summary>
    private static void SimulateNewEpicArchive(FakeProcessRunner git, Harness h) =>
        git.OnRunAsync = (fileName, _, _) => fileName == "git"
            ? Task.CompletedTask
            : Task.WhenAll(
                h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)),
                h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details)),
                h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext)),
                h.Store.DeleteAsync(Resolve(h.Repo, MilestonePath("m1.md"))));

    private static async Task SeedCompletePreviousWorkspaceAsync(Harness h)
    {
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "OLD PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "OLD DETAILS");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OLD CTX");
        await h.Store.WriteAsync(Resolve(h.Repo, MilestonePath("m1.md")), "- [x] done");
    }

    [Fact]
    public async Task RunAsync_CompletePreviousWorkspace_ArchivesAndPublishesBeforePreflight_ThenPlansTheNextEpicFromTheSurvivingEpic()
    {
        FakeProcessRunner git = DirtyGit();
        Harness h = New(git);
        // A complete previous-epic workspace, plus the epic that new-epic leaves in place under specs/.
        await SeedCompletePreviousWorkspaceAsync(h);
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic), "NEXT EPIC");
        SimulateNewEpicArchive(git, h);
        ScriptFullHappyRun(h);

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        // The rollover ran BEFORE preflight, and preflight evaluated the POST-archive state: the old artifacts
        // are gone (no "already exists") and specs/epic.md SURVIVED the archive — so the SAME run proceeds
        // to plan the next epic from it.
        Assert.Equal(PlanOutcome.Completed, outcome);
        Assert.Equal(
            new[]
            {
                "Epic Rollover", "Preflight", "Write Plan", "Adversarial Review", "Revise Plan",
                "Collect Details", "Extract Milestones", "Extract Details",
            },
            PhaseSequence(h.Console));
        Assert.DoesNotContain(h.Console.Events, e => e.Kind == "error");
        Assert.Equal(
            "NEXT EPIC",
            await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.SpecsEpic)));

        // new-epic ran exactly once, against the provided directory, and BEFORE any publish (its archive is
        // what the first commit records). (Matched as the only non-git invocation — robust to the
        // NEW_EPIC_EXECUTABLE resolution tests running in parallel elsewhere in the assembly.)
        var newEpic = Assert.Single(h.Processes.Calls, c => c.FileName != "git");
        Assert.Equal(h.Repo.Path, newEpic.WorkingDirectory);
        int newEpicIndex = h.Processes.Calls.FindIndex(c => c.FileName != "git");
        int firstCommitIndex = h.Processes.Calls.FindIndex(c => c.FileName == "git" && c.Args[0] == "commit");
        Assert.True(newEpicIndex < firstCommitIndex, "new-epic must run before the archive publish");

        // The full ordered commit cadence: the archive's submodule commit AND its IMMEDIATE parent gitlink
        // pointer land first (before preflight could have blocked), then one submodule commit per
        // artifact-mutating step, then exactly ONE more parent gitlink commit at the very end.
        var allCommits = git.Calls
            .Where(c => c.FileName == "git" && c.Args[0] == "commit")
            .Select(c => (Message: c.Args[2], Parent: !IsSubmodule(c.WorkingDirectory)))
            .ToList();
        Assert.Equal(
            new[]
            {
                (AgentsSubmodulePublisher.ArchivePreviousEpicMessage, false),
                (AgentsSubmodulePublisher.GitlinkPointerMessage, true),
                (AgentsSubmodulePublisher.WritePlanMessage, false),
                (AgentsSubmodulePublisher.RevisePlanMessage, false),
                (AgentsSubmodulePublisher.CollectDetailsMessage, false),
                (AgentsSubmodulePublisher.ExtractMilestonesMessage, false),
                (AgentsSubmodulePublisher.ExtractDetailsMessage, false),
                (AgentsSubmodulePublisher.GitlinkPointerMessage, true),
            },
            allCommits);
    }

    [Fact]
    public async Task RunAsync_CompletePreviousWorkspace_ButNoEpicEverExisted_ArchivesAndPublishes_ThenPreflightBlocks()
    {
        FakeProcessRunner git = DirtyGit();
        Harness h = New(git);
        // A complete previous-epic workspace but specs/epic.md was NEVER authored: the rollover still
        // archives (and publishes) the old workspace, then preflight blocks on the missing epic.
        await SeedCompletePreviousWorkspaceAsync(h);
        SimulateNewEpicArchive(git, h);

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        // Preflight evaluated the POST-archive state: its only violation is the missing epic ("author the
        // next one and rerun"), never "already exists".
        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(new[] { "Epic Rollover", "Preflight" }, PhaseSequence(h.Console));
        Assert.Contains(
            h.Console.Events,
            e => e.Kind == "error" && e.Text.Contains(OrchestrationArtifactPaths.SpecsEpic) && e.Text.Contains("not found"));
        Assert.DoesNotContain(h.Console.Events, e => e.Kind == "error" && e.Text.Contains("already exists"));

        // new-epic ran exactly once, against the provided directory. (Matched as the only non-git invocation —
        // robust to the NEW_EPIC_EXECUTABLE resolution tests running in parallel elsewhere in the assembly.)
        var newEpic = Assert.Single(h.Processes.Calls, c => c.FileName != "git");
        Assert.Equal(h.Repo.Path, newEpic.WorkingDirectory);

        // The archive's submodule commit AND its parent gitlink pointer both landed even though the run then
        // blocked at preflight.
        Assert.Contains(h.Processes.Calls, c =>
            c.FileName == "git" && IsSubmodule(c.WorkingDirectory) &&
            c.Args.SequenceEqual(new[] { "commit", "-m", AgentsSubmodulePublisher.ArchivePreviousEpicMessage }));
        Assert.Contains(h.Processes.Calls, c =>
            c.FileName == "git" && !IsSubmodule(c.WorkingDirectory) &&
            c.Args.SequenceEqual(new[] { "commit", "-m", AgentsSubmodulePublisher.GitlinkPointerMessage }));

        // Zero codex calls: the run never got past preflight.
        Assert.Equal(0, h.Runtime.OpenSessions);
        Assert.Empty(h.Runtime.OneShotCalls);
    }

    [Fact]
    public async Task RunAsync_IncompletePreviousWorkspace_SkipsNewEpic_AndPreflightReportsTheExistingViolations()
    {
        Harness h = New();
        // plan.md only — an incomplete previous workspace is deliberately NOT auto-archived.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "STRAY PLAN");

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        // No new-epic invocation, and nothing published before a blocked preflight — no process ran at all.
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
        h.Runtime.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "stderr tail")));

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.Failed, outcome);

        // The steps that completed before the failure published their submodule commits...
        var submoduleCommits = git.Calls
            .Where(c => c.FileName == "git" && IsSubmodule(c.WorkingDirectory) && c.Args[0] == "commit")
            .Select(c => c.Args[2])
            .ToList();
        Assert.Equal(
            new[] { AgentsSubmodulePublisher.WritePlanMessage, AgentsSubmodulePublisher.RevisePlanMessage },
            submoduleCommits);

        // ...but the parent gitlink pointer is recorded only at the END of a successful run: a failed run
        // makes NO parent-repo git call whatsoever.
        Assert.DoesNotContain(git.Calls, c => !IsSubmodule(c.WorkingDirectory));
    }

    [Fact]
    public async Task RunAsync_WhenTheRolloverArchives_ClearsThePersistedDecisionSessionState()
    {
        Harness h = New();
        // A COMPLETE previous workspace (presence-based criterion): plan + details + operational context +
        // a non-empty milestones directory. The scripted new-epic invocation deletes plan.md so the
        // rollover's post-gate passes; the run then stops at Preflight (no specs/epic.md) — which is fine,
        // the clear must already have happened.
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await h.Store.WriteAsync(Resolve(h.Repo, MilestonePath("m1.md")), "- [ ] t");
        h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);
        h.Processes.Handler = (_, args) =>
        {
            if (args.Count == 0 || args.Contains("new-epic"))
            {
                h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)).Wait();
            }

            return FakeProcessRunner.Ok();
        };

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(1, h.Resume.ClearCalls);
        Assert.Null(h.Resume.State);
    }

    [Fact]
    public async Task RunAsync_WhenNoRolloverHappens_LeavesThePersistedDecisionSessionStateAlone()
    {
        Harness h = New();
        // An incomplete workspace: the rollover is skipped and preflight blocks. The resume state survives.
        h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);

        PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
        Assert.Equal(0, h.Resume.ClearCalls);
        Assert.NotNull(h.Resume.State);
    }
}

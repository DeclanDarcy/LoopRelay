using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Execution;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using LoopRelay.Plan.Cli.Tests.Models;
using LoopRelay.Plan.Cli.Tests.Services.Agents;
using LoopRelay.Plan.Cli.Tests.Services.Support;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class ReviewStepTests
{
    private static (ReviewStep Step, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new ReviewStep(rt, artifacts, con, repo), rt, store, repo, con);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static Task SeedPlanAsync(MemoryArtifactStore store, Repository repo, string content) =>
        store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), content);

    [Fact]
    public async Task RunAsync_ReturnsTurnOutputVerbatim()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN CONTENT");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("REVIEW OUTPUT")));

        string output = await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Equal("REVIEW OUTPUT", output);
    }

    [Fact]
    public async Task RunAsync_SendsAdversarialPlanReviewRenderedWithPlanAndImplementationFirstSemantics()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN CONTENT");
        string? capturedPrompt = null;
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
        {
            capturedPrompt = prompt;
            return Turns.Completed("REVIEW OUTPUT");
        }));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.NotNull(capturedPrompt);
        AdversarialPlanReviewPromptTestAssertions.AssertContainsImplementationFirstReviewSemantics(capturedPrompt);
        AdversarialPlanReviewPromptTestAssertions.AssertNoUnresolvedPlaceholders(capturedPrompt);
        Assert.DoesNotContain("## Implementation-First Prompt Policy", capturedPrompt ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("PROJECT CONTEXT PROJECTION", capturedPrompt);
        Assert.Contains("PLAN CONTENT", capturedPrompt);
    }

    [Fact]
    public async Task RunAsync_OpensReadOnlySessionAtRepoRootWithXhighEffort()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("read-only", spec.Sandbox.Identifier);
            Assert.False(spec.Sandbox.CanWriteWorkspace);
            Assert.False(spec.Sandbox.CanAccessNetwork);
            Assert.False(spec.Sandbox.RequiresApproval);
            Assert.Equal(AgentEffort.XHigh, spec.Effort);
            Assert.Equal(repo.Path, spec.WorkingDirectory);
            Assert.Equal(SessionRole.Planning, spec.Role);
            return Turns.Completed("REVIEW OUTPUT");
        }));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);
    }

    [Fact]
    public async Task RunAsync_ClosesSessionOnSuccess()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("REVIEW OUTPUT")));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_TurnFailed_ThrowsWithDiagnosticsTail_AndClosesSession()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom", "review stderr tail")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None));

        Assert.Contains("review stderr tail", ex.Message);
        Assert.Contains("Agent stderr (tail):", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_TurnFailedWithoutDiagnostics_ThrowsWithoutDiagnosticsSuffix_AndClosesSession()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed("boom")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None));

        Assert.DoesNotContain("Agent stderr (tail):", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_WhitespaceOnlyOutput_Throws_AndClosesSession()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("   \n  ")));

        PlanStepException ex = await Assert.ThrowsAsync<PlanStepException>(
            () => step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None));

        Assert.Contains("adversarial review returned no output", ex.Message);
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_PlanMissing_ThrowsBeforeOpeningSession()
    {
        var (step, rt, _, _, _) = New();

        await Assert.ThrowsAsync<PlanStepException>(() => step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None));

        Assert.Equal(0, rt.OpenSessions);
        Assert.Equal(0, rt.ClosedSessions);
    }

    [Fact]
    public async Task RunAsync_PlanIsWhitespaceOnly_ThrowsBeforeOpeningSession()
    {
        var (step, rt, store, repo, _) = New();
        await SeedPlanAsync(store, repo, "   \n  ");

        await Assert.ThrowsAsync<PlanStepException>(() => step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None));

        Assert.Equal(0, rt.OpenSessions);
    }

    [Fact]
    public async Task RunAsync_ReportsVerdict_WhenFailBulletPresent()
    {
        var (step, rt, store, repo, con) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Completed("## Verdict\n\n- FAIL: not ready.")));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "info" && e.Text == "Review verdict: FAIL");
    }

    [Fact]
    public async Task RunAsync_ReportsVerdict_WhenConditionalPassBulletPresent()
    {
        var (step, rt, store, repo, con) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Completed("## Verdict\n\n- CONDITIONAL PASS: fix the blockers first.")));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "info" && e.Text == "Review verdict: CONDITIONAL PASS");
    }

    [Fact]
    public async Task RunAsync_ReportsVerdict_WhenPassBulletPresent()
    {
        var (step, rt, store, repo, con) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Completed("## Verdict\n\n- PASS: no material execution risks found.")));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "info" && e.Text == "Review verdict: PASS");
    }

    [Fact]
    public async Task RunAsync_WarnsWhenVerdictAbsent()
    {
        var (step, rt, store, repo, con) = New();
        await SeedPlanAsync(store, repo, "PLAN");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            Turns.Completed("the review body has no verdict section")));

        await step.RunAsync("PROJECT CONTEXT PROJECTION", CancellationToken.None);

        Assert.Contains(con.Events, e => e.Kind == "warn" && e.Text == "Review verdict not found in output.");
    }

    [Theory]
    [InlineData("- FAIL: reasoning here.", "FAIL")]
    [InlineData("- CONDITIONAL PASS: reasoning here.", "CONDITIONAL PASS")]
    [InlineData("- PASS: reasoning here.", "PASS")]
    public void TryExtractVerdict_FindsEachCanonicalBullet(string line, string expectedVerdict)
    {
        bool found = ReviewStep.TryExtractVerdict($"## Verdict\r\n\r\n{line}\r\n", out string verdict);

        Assert.True(found);
        Assert.Equal(expectedVerdict, verdict);
    }

    [Fact]
    public void TryExtractVerdict_ReturnsFalse_WhenNoBulletPresent()
    {
        bool found = ReviewStep.TryExtractVerdict("## Verdict\r\n\r\nSomething else entirely.\r\n", out string verdict);

        Assert.False(found);
        Assert.Equal(string.Empty, verdict);
    }

    [Fact]
    public void TryExtractVerdict_FirstMatchingLineWins_WhenMultipleBulletsPresent()
    {
        bool found = ReviewStep.TryExtractVerdict(
            "- CONDITIONAL PASS: fix blockers.\n- PASS: no risks.\n- FAIL: not ready.", out string verdict);

        Assert.True(found);
        Assert.Equal("CONDITIONAL PASS", verdict);
    }
}

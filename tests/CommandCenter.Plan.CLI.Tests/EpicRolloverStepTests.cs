using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Plan.Cli;
using Xunit;

namespace CommandCenter.Plan.Cli.Tests;

/// <summary>
/// Both NEW_EPIC_EXECUTABLE resolution tests live in THIS class deliberately: xunit runs tests within one
/// class serially, so mutating the process-wide environment variable cannot race the default-resolution test.
/// </summary>
public class EpicRolloverStepTests
{
    private const string OverrideVariable = "NEW_EPIC_EXECUTABLE";

    private sealed record Harness(
        EpicRolloverStep Step,
        FakeProcessRunner Runner,
        MemoryArtifactStore Store,
        Repository Repo,
        RecordingLoopConsole Console);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var artifacts = new PlanArtifacts(store, repo);
        var console = new RecordingLoopConsole();
        var runner = new FakeProcessRunner();
        return new Harness(new EpicRolloverStep(runner, artifacts, console, repo), runner, store, repo, console);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    private static string MilestonePath(Repository repo) => Resolve(
        repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1.md"));

    /// <summary>Seeds the complete previous-epic workspace, optionally omitting one repository-relative path
    /// (pass "milestones" to omit the milestone file).</summary>
    private static async Task SeedWorkspaceAsync(Harness h, string? omit = null)
    {
        if (omit != OrchestrationArtifactPaths.Plan)
        {
            await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        }

        if (omit != OrchestrationArtifactPaths.Details)
        {
            await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS");
        }

        if (omit != OrchestrationArtifactPaths.OperationalContext)
        {
            await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "CTX");
        }

        if (omit != "milestones")
        {
            await h.Store.WriteAsync(MilestonePath(h.Repo), "- [ ] a");
        }
    }

    /// <summary>Wires the fake so the new-epic call "archives" plan.md, satisfying the post-gate.</summary>
    private static void SimulateArchiveOnRun(Harness h) =>
        h.Runner.OnRunAsync = (fileName, _, _) => fileName == "git"
            ? Task.CompletedTask
            : h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan));

    private static async Task<T> WithOverrideAsync<T>(string? value, Func<Task<T>> body)
    {
        string? saved = Environment.GetEnvironmentVariable(OverrideVariable);
        try
        {
            Environment.SetEnvironmentVariable(OverrideVariable, value);
            return await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(OverrideVariable, saved);
        }
    }

    [Theory]
    [InlineData(OrchestrationArtifactPaths.Plan)]
    [InlineData(OrchestrationArtifactPaths.Details)]
    [InlineData(OrchestrationArtifactPaths.OperationalContext)]
    [InlineData("milestones")]
    public async Task TryArchiveAsync_AnyWorkspacePieceMissing_ReturnsFalse_AndRunsNoProcess(string omit)
    {
        Harness h = New();
        await SeedWorkspaceAsync(h, omit);

        bool archived = await h.Step.TryArchiveAsync(CancellationToken.None);

        Assert.False(archived);
        Assert.Empty(h.Runner.Calls);
        Assert.Contains(h.Console.Events, e => e.Kind == "info" && e.Text.Contains("nothing to archive"));
    }

    [Fact]
    public async Task TryArchiveAsync_CompleteWorkspace_InvokesNewEpicViaCmd_InRepositoryPath_AndReturnsTrue()
    {
        Harness h = New();
        await SeedWorkspaceAsync(h);
        SimulateArchiveOnRun(h);
        h.Runner.Handler = (_, _) => FakeProcessRunner.Ok("New Epic Complete\nCertification: PASSED");

        bool archived = await WithOverrideAsync<bool>(null, () => h.Step.TryArchiveAsync(CancellationToken.None));

        Assert.True(archived);
        // `new-epic` is only reachable through a .bat alias, which CreateProcess cannot resolve via PATHEXT —
        // so the default launch goes through cmd.exe /c, in the provided directory (new-epic resolves the
        // repository root upward from its working directory; it accepts no arguments).
        var call = Assert.Single(h.Runner.Calls);
        Assert.Equal("cmd.exe", call.FileName);
        Assert.Equal(new[] { "/c", "new-epic" }, call.Args);
        Assert.Equal(h.Repo.Path, call.WorkingDirectory);
        // The tool's stdout summary is echoed.
        Assert.Contains(h.Console.Events, e => e.Kind == "info" && e.Text.Contains("Certification: PASSED"));
    }

    [Fact]
    public async Task TryArchiveAsync_NewEpicExecutableOverride_LaunchesThatFileDirectly_WithNoArguments()
    {
        Harness h = New();
        await SeedWorkspaceAsync(h);
        SimulateArchiveOnRun(h);

        bool archived = await WithOverrideAsync(
            @"C:\tools\new-epic-cli\new-epic.exe", () => h.Step.TryArchiveAsync(CancellationToken.None));

        Assert.True(archived);
        var call = Assert.Single(h.Runner.Calls);
        Assert.Equal(@"C:\tools\new-epic-cli\new-epic.exe", call.FileName);
        Assert.Empty(call.Args);
        Assert.Equal(h.Repo.Path, call.WorkingDirectory);
    }

    [Fact]
    public async Task TryArchiveAsync_NonZeroExit_Throws_WithExitCodeAndStderr()
    {
        Harness h = New();
        await SeedWorkspaceAsync(h);
        h.Runner.Handler = (_, _) => FakeProcessRunner.Fail("certification FAILED: dangling handoff");

        var ex = await Assert.ThrowsAsync<PlanStepException>(
            () => WithOverrideAsync<bool>(null, () => h.Step.TryArchiveAsync(CancellationToken.None)));

        Assert.Contains("exit code 1", ex.Message);
        Assert.Contains("certification FAILED: dangling handoff", ex.Message);
    }

    [Fact]
    public async Task TryArchiveAsync_NonZeroExit_BlankStderr_FallsBackToStdout()
    {
        Harness h = New();
        await SeedWorkspaceAsync(h);
        h.Runner.Handler = (_, _) => FakeProcessRunner.Fail(string.Empty, exitCode: 2, stdout: "only stdout details");

        var ex = await Assert.ThrowsAsync<PlanStepException>(
            () => WithOverrideAsync<bool>(null, () => h.Step.TryArchiveAsync(CancellationToken.None)));

        Assert.Contains("exit code 2", ex.Message);
        Assert.Contains("only stdout details", ex.Message);
    }

    [Fact]
    public async Task TryArchiveAsync_ZeroExitButPlanStillPresent_Throws()
    {
        // The deterministic post-gate: a "successful" new-epic that did not actually move plan.md into the
        // archive is a loud failure, never a silent pass-through into a preflight violation.
        Harness h = New();
        await SeedWorkspaceAsync(h);
        // No OnRunAsync side effect: plan.md survives the zero-exit run.

        var ex = await Assert.ThrowsAsync<PlanStepException>(
            () => WithOverrideAsync<bool>(null, () => h.Step.TryArchiveAsync(CancellationToken.None)));

        Assert.Contains(OrchestrationArtifactPaths.Plan, ex.Message);
        Assert.Contains("was not archived", ex.Message);
    }
}

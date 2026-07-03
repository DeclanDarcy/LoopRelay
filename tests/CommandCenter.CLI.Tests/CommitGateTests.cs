using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class CommitGateTests
{
    private const string CommitMessage = "Orchestration loop: automated execution and decision iteration";

    // git add stages everything except the `.agents` submodule — the gate never touches the gitlink.
    private static readonly string[] AddExcludingAgents = ["add", "-A", "--", ".", ":(exclude).agents"];

    private static CommitGate New(FakeProcessRunner fake)
    {
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return new CommitGate(new WorkingTreeChangeDetector(fake, repo), fake, repo, new RecordingLoopConsole());
    }

    /// <summary>Scripts a runner whose `git status` always returns the given porcelain; everything else succeeds.</summary>
    private static FakeProcessRunner StatusRunner(string porcelain) => new()
    {
        Handler = (_, args) => args[0] == "status"
            ? FakeProcessRunner.Ok(porcelain)
            : FakeProcessRunner.Ok()
    };

    // The parent working tree only ever surfaces the submodule as a lone `.agents` gitlink, which CommitGate
    // ignores — so an iteration that shows only this made no real progress.
    private const string GitlinkOnly = " M .agents";

    [Fact]
    public async Task T1_OnlyAgentsGitlink_TripsAfterThreshold()
    {
        var gate = New(StatusRunner(GitlinkOnly));

        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));  // count 3 -> 3 > 2

        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T2_RealSourceChange_ResetsCounter()
    {
        var fake = StatusRunner(GitlinkOnly);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 2

        // A real (non-.agents) source change — even alongside the ignored gitlink — resets the counter to 0.
        fake.Handler = (_, args) => args[0] == "status"
            ? FakeProcessRunner.Ok(" M .agents\n M src/Foo.cs")
            : FakeProcessRunner.Ok();
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));
        Assert.Equal(0, gate.NoChangesCount);

        // Prove the reset really happened: it takes 3 MORE gitlink-only iterations to trip again.
        fake.Handler = (_, args) => args[0] == "status"
            ? FakeProcessRunner.Ok(GitlinkOnly)
            : FakeProcessRunner.Ok();
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T3_CommitsAndPushes_ExcludingAgents_WhenRealChangesPresent()
    {
        // The gitlink is present but ignored; the real source change drives the commit.
        var fake = StatusRunner(" M .agents\n M src/Foo.cs");
        var gate = New(fake);

        await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None);

        Assert.Equal(4, fake.Calls.Count);
        Assert.All(fake.Calls, call => Assert.Equal("git", call.FileName));
        Assert.Equal(new[] { "status", "--porcelain" }, fake.Calls[0].Args);
        // The add EXCLUDES the `.agents` submodule so the gitlink is never staged/committed.
        Assert.Equal(AddExcludingAgents, fake.Calls[1].Args);
        Assert.Equal(new[] { "commit", "-m", CommitMessage }, fake.Calls[2].Args);
        Assert.Equal(new[] { "push" }, fake.Calls[3].Args);
    }

    [Fact]
    public async Task T4_EmptyChangeset_SkipsCommitButCountsAsNoProgress()
    {
        var fake = StatusRunner(string.Empty);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 1

        // Only `git status` was ever invoked — no add/commit/push.
        Assert.Single(fake.Calls);
        Assert.Equal(new[] { "status", "--porcelain" }, fake.Calls[0].Args);

        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T5_PushFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M src/Foo.cs"),
                "push" => FakeProcessRunner.Fail("push rejected"),
                _ => FakeProcessRunner.Ok()
            }
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));
    }

    [Fact]
    public async Task T6_CommitFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M src/Foo.cs"),
                "commit" => FakeProcessRunner.Fail("nothing to commit"),
                _ => FakeProcessRunner.Ok()
            }
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));
    }

    [Fact]
    public async Task T7_StatusFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] == "status"
                ? FakeProcessRunner.Fail("not a git repository")
                : FakeProcessRunner.Ok()
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));

        // The failure happens at `status`, before any add/commit/push.
        Assert.Single(fake.Calls);
        Assert.Equal("status", fake.Calls[0].Args[0]);
    }

    [Fact]
    public async Task T8_AgentsSubpaths_AreIgnored_NotCountedAsProgress()
    {
        // Defensive: even if a `.agents/<file>` path surfaced at the parent, it is ignored like the gitlink.
        var gate = New(StatusRunner(" M .agents\n M .agents/operational_context.md"));

        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(0, 0, CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T9_MilestoneReduction_WithoutRepoChanges_ResetsCounter_WithoutCommitting()
    {
        var fake = StatusRunner(GitlinkOnly);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(5, 5, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(5, 5, CancellationToken.None)); // count 2

        // Ticked milestone boxes (5 open -> 4 open) with a clean working tree reset the counter to 0,
        // exactly like a real code change would...
        Assert.False(await gate.CommitPushAndEvaluateAsync(5, 4, CancellationToken.None));
        Assert.Equal(0, gate.NoChangesCount);
        // ...but there is nothing to commit: only `git status` probes ever ran — no add/commit/push.
        Assert.All(fake.Calls, call => Assert.Equal("status", call.Args[0]));

        // Prove the reset really happened: it takes 3 MORE no-progress iterations to trip again.
        Assert.False(await gate.CommitPushAndEvaluateAsync(4, 4, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(4, 4, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(4, 4, CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T10_MilestoneIncrease_IsNotProgress()
    {
        // An execution that only ADDED milestone items (4 open -> 6 open) made no progress toward the
        // epic — only a reduction counts.
        var gate = New(StatusRunner(GitlinkOnly));

        Assert.False(await gate.CommitPushAndEvaluateAsync(4, 6, CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(6, 6, CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(6, 6, CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }
}

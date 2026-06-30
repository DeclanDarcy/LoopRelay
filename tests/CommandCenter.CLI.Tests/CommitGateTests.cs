using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class CommitGateTests
{
    private const string CommitMessage = "Orchestration loop: automated execution and decision iteration";

    private static CommitGate New(FakeProcessRunner fake) =>
        new(fake, new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" }, new RecordingLoopConsole());

    /// <summary>Scripts a runner whose `git status` always returns the given porcelain; everything else succeeds.</summary>
    private static FakeProcessRunner StatusRunner(string porcelain) => new()
    {
        Handler = args => args[0] == "status"
            ? FakeProcessRunner.Ok(porcelain)
            : FakeProcessRunner.Ok()
    };

    // Two bookkeeping paths (decisions + handoff) — the every-iteration churn that must NOT count as progress.
    private const string Bookkeeping =
        " M .agents/decisions/decisions.md\n M .agents/handoffs/handoff.md";

    [Fact]
    public async Task T1_OnlyBookkeeping_TripsAfterThreshold()
    {
        var fake = StatusRunner(Bookkeeping);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(CancellationToken.None));  // count 3 -> 3 > 2

        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T2_NonBookkeepingChange_ResetsCounter()
    {
        var fake = StatusRunner(Bookkeeping);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 2

        // A real source change resets the counter to 0.
        fake.Handler = args => args[0] == "status"
            ? FakeProcessRunner.Ok(" M .agents/decisions/decisions.md\n M src/Foo.cs")
            : FakeProcessRunner.Ok();
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None));
        Assert.Equal(0, gate.NoChangesCount);

        // Prove the reset really happened: it takes 3 MORE bookkeeping iterations to trip again.
        fake.Handler = args => args[0] == "status"
            ? FakeProcessRunner.Ok(Bookkeeping)
            : FakeProcessRunner.Ok();
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T3_CommitsAndPushes_WhenChangesPresent()
    {
        var fake = StatusRunner(" M src/Foo.cs");
        var gate = New(fake);

        await gate.CommitPushAndEvaluateAsync(CancellationToken.None);

        Assert.Equal(4, fake.Calls.Count);
        Assert.All(fake.Calls, call => Assert.Equal("git", call.FileName));
        Assert.Equal(new[] { "status", "--porcelain" }, fake.Calls[0].Args);
        Assert.Equal(new[] { "add", "-A" }, fake.Calls[1].Args);
        Assert.Equal(new[] { "commit", "-m", CommitMessage }, fake.Calls[2].Args);
        Assert.Equal(new[] { "push" }, fake.Calls[3].Args);

        // The commit message argument must be the exact constant.
        Assert.Equal(CommitMessage, fake.Calls[2].Args[2]);
    }

    [Fact]
    public async Task T4_EmptyChangeset_SkipsCommitButCountsAsNoProgress()
    {
        var fake = StatusRunner(string.Empty);
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 1

        // Only `git status` was ever invoked — no add/commit/push.
        Assert.Single(fake.Calls);
        Assert.Equal(new[] { "status", "--porcelain" }, fake.Calls[0].Args);

        // The empty changeset still counts as no-progress and trips on the 3rd iteration.
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }

    [Fact]
    public async Task T5_PushFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = args => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(Bookkeeping),
                "push" => FakeProcessRunner.Fail("push rejected"),
                _ => FakeProcessRunner.Ok()
            }
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task T6_CommitFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = args => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(Bookkeeping),
                "commit" => FakeProcessRunner.Fail("nothing to commit"),
                _ => FakeProcessRunner.Ok()
            }
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task T7_StatusFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = args => args[0] == "status"
                ? FakeProcessRunner.Fail("not a git repository")
                : FakeProcessRunner.Ok()
        };
        var gate = New(fake);

        await Assert.ThrowsAsync<LoopStepException>(
            () => gate.CommitPushAndEvaluateAsync(CancellationToken.None));

        // The failure happens at `status`, before any add/commit/push.
        Assert.Single(fake.Calls);
        Assert.Equal("status", fake.Calls[0].Args[0]);
    }

    [Fact]
    public async Task T8_OperationalContextChange_DoesNotCountAsProgress()
    {
        // An iteration that touched only decisions + .agents/operational_context.md is still no-progress:
        // operational_context is orchestration bookkeeping, not substantive milestone work. So the counter
        // keeps climbing and trips on the 3rd iteration rather than resetting (which is what a real source
        // change would do — see T2).
        var fake = StatusRunner(" M .agents/decisions/decisions.md\n M .agents/operational_context.md");
        var gate = New(fake);

        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 1
        Assert.False(await gate.CommitPushAndEvaluateAsync(CancellationToken.None)); // count 2
        Assert.True(await gate.CommitPushAndEvaluateAsync(CancellationToken.None));  // count 3 -> trip
        Assert.Equal(3, gate.NoChangesCount);
    }
}

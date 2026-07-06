using LoopRelay.Core.Repositories;
using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class AgentsSubmodulePublisherTests
{
    private const string Message = Cli.AgentsSubmodulePublisher.WritePlanMessage;

    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    private static bool IsSubmodule(string workingDirectory) =>
        workingDirectory.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);

    private static Cli.AgentsSubmodulePublisher New(FakeProcessRunner fake) =>
        new(fake, Repo, new RecordingLoopConsole());

    /// <summary>Scripts the submodule git: a status porcelain and a branch name; everything else succeeds.</summary>
    private static FakeProcessRunner Runner(string status, string branch = "main") => new()
    {
        Handler = (_, args) => args[0] switch
        {
            "status" => FakeProcessRunner.Ok(status),
            "branch" => FakeProcessRunner.Ok(branch),
            _ => FakeProcessRunner.Ok()
        }
    };

    [Fact]
    public async Task PublishAgentsAsync_CleanAndUpToDate_ReturnsFalse_AndCommitsNothing()
    {
        var fake = Runner(status: string.Empty); // rev-list falls through to Ok("") -> not ahead of upstream

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.False(committed);
        // Nothing to commit and nothing to recover -> no add/commit/push.
        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "add" or "commit" or "push");
        Assert.Contains(fake.Calls, c => c.Args[0] == "status");
    }

    [Fact]
    public async Task PublishAgentsAsync_CleanButAheadOfUpstream_PushesStrandedCommit_WithoutCommitting()
    {
        // Working tree clean, but a prior failed push left HEAD ahead of upstream (`rev-list --count` > 0).
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(string.Empty),
                "rev-list" => FakeProcessRunner.Ok("1"),
                "branch" => FakeProcessRunner.Ok("main"),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.False(committed); // no NEW commit was made
        // The stranded commit is pushed (inside the submodule), without staging or committing again.
        Assert.Contains(fake.Calls, c => c.Args[0] == "push" && IsSubmodule(c.WorkingDirectory));
        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "add" or "commit");
    }

    [Fact]
    public async Task PublishAgentsAsync_DirtyOnBranch_CommitsAndPushes_AtSubmoduleWorkingDirectory()
    {
        var fake = Runner(status: " M plan.md");

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.All(fake.Calls, c => Assert.Equal("git", c.FileName));

        // The submodule commit+push runs inside the `.agents` working directory, in this exact order.
        var submoduleCalls = fake.Calls.Where(c => IsSubmodule(c.WorkingDirectory)).ToList();
        Assert.Equal(new[] { "status", "--porcelain" }, submoduleCalls[0].Args);
        Assert.Equal(new[] { "branch", "--show-current" }, submoduleCalls[1].Args);
        Assert.Equal(new[] { "add", "-A" }, submoduleCalls[2].Args);
        Assert.Equal(new[] { "commit", "-m", Message }, submoduleCalls[3].Args);
        Assert.Equal(new[] { "push" }, submoduleCalls[4].Args);
    }

    [Fact]
    public async Task PublishAgentsAsync_MakesNoParentRepoGitCalls_EvenWhenItCommits()
    {
        // The split API: publishing the submodule NEVER touches the parent repo — the pipeline decides when
        // the moved gitlink pointer is recorded, via RecordParentGitlinkAsync.
        var fake = Runner(status: " M plan.md");

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.All(fake.Calls, c => Assert.True(IsSubmodule(c.WorkingDirectory)));
    }

    [Fact]
    public async Task PublishAgentsAsync_CommitMessage_IsPassedThrough()
    {
        var fake = Runner(status: " M details.md");

        await New(fake).PublishAgentsAsync(Cli.AgentsSubmodulePublisher.ExtractDetailsMessage, CancellationToken.None);

        var commit = fake.Calls.Single(c => c.Args[0] == "commit");
        Assert.Equal(new[] { "commit", "-m", Cli.AgentsSubmodulePublisher.ExtractDetailsMessage }, commit.Args);
    }

    [Fact]
    public async Task PublishAgentsAsync_DetachedHead_Throws_WithoutCommittingOrPushing()
    {
        // Dirty tree but detached HEAD (blank `branch --show-current`) cannot be pushed.
        var fake = Runner(status: " M plan.md", branch: string.Empty);

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).PublishAgentsAsync(Message, CancellationToken.None));

        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "commit" or "push");
    }

    [Fact]
    public async Task PublishAgentsAsync_PushFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M plan.md"),
                "branch" => FakeProcessRunner.Ok("main"),
                "push" => FakeProcessRunner.Fail("push rejected"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).PublishAgentsAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAgentsAsync_PushFailure_WhenRetrySucceeds_ReturnsTrue()
    {
        int pushes = 0;
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M plan.md"),
                "branch" => FakeProcessRunner.Ok("main"),
                "push" => ++pushes == 1
                    ? FakeProcessRunner.Fail("Recv failure: Connection was reset")
                    : FakeProcessRunner.Ok(),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.Equal(2, pushes);
    }

    [Fact]
    public async Task PublishAgentsAsync_PushFailure_WhenUpstreamAlreadyHasHead_IsTreatedAsPushed()
    {
        const string head = "918eca5cc5762e21c267303838fa2dcec1461963";
        // HEAD and @{u} are stubbed as SEPARATE commands (full-args dispatch) so this test proves the
        // fallback compares the two probes' outputs, not that a single blanket "rev-parse" stub matched.
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args switch
            {
                ["status", ..] => FakeProcessRunner.Ok(" M plan.md"),
                ["branch", ..] => FakeProcessRunner.Ok("main"),
                ["push"] => FakeProcessRunner.Fail("Recv failure: Connection was reset"),
                ["fetch", ..] => FakeProcessRunner.Ok(),
                ["rev-parse", "HEAD"] => FakeProcessRunner.Ok(head),
                ["rev-parse", "@{u}"] => FakeProcessRunner.Ok(head),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAgentsAsync(Message, CancellationToken.None);

        Assert.True(committed);
        // Push not retried once the upstream probe proved HEAD already landed.
        Assert.Single(fake.Calls, c => c.Args.SequenceEqual(new[] { "push" }));
        // Both sides of the sha comparison were actually consulted.
        Assert.Contains(fake.Calls, c => c.Args.SequenceEqual(new[] { "rev-parse", "HEAD" }));
        Assert.Contains(fake.Calls, c => c.Args.SequenceEqual(new[] { "rev-parse", "@{u}" }));
    }

    [Fact]
    public async Task PublishAgentsAsync_PushFailure_WhenUpstreamIsAtADifferentSha_RetriesThenThrows()
    {
        // Both shas resolve (non-empty) but DIFFER: the rejected push must not be treated as "already
        // present on upstream". The publish retries the push once and then fails for real.
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args switch
            {
                ["status", ..] => FakeProcessRunner.Ok(" M plan.md"),
                ["branch", ..] => FakeProcessRunner.Ok("main"),
                ["push"] => FakeProcessRunner.Fail("push rejected"),
                ["fetch", ..] => FakeProcessRunner.Ok(),
                ["rev-parse", "HEAD"] => FakeProcessRunner.Ok("918eca5cc5762e21c267303838fa2dcec1461963"),
                ["rev-parse", "@{u}"] => FakeProcessRunner.Ok("a75de6f0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).PublishAgentsAsync(Message, CancellationToken.None));

        Assert.Equal(2, fake.Calls.Count(c => c.Args.SequenceEqual(new[] { "push" })));
    }

    [Fact]
    public async Task PublishAgentsAsync_CommitFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M plan.md"),
                "branch" => FakeProcessRunner.Ok("main"),
                "commit" => FakeProcessRunner.Fail("nothing to commit"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).PublishAgentsAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAgentsAsync_StatusFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] == "status"
                ? FakeProcessRunner.Fail("not a git repository")
                : FakeProcessRunner.Ok()
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).PublishAgentsAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task RecordParentGitlinkAsync_RunsAddCommitPush_InParentRepo_StagingOnlyAgents()
    {
        var fake = new FakeProcessRunner();

        await New(fake).RecordParentGitlinkAsync(CancellationToken.None);

        // All three calls run in the repo root (never the submodule), stage ONLY the `.agents` gitlink
        // (not `-A`), and push — no status probe anywhere: the caller's control flow gates this.
        Assert.Equal(3, fake.Calls.Count);
        Assert.All(fake.Calls, c => Assert.Equal("/repo", c.WorkingDirectory.Replace('\\', '/')));
        Assert.All(fake.Calls, c => Assert.Equal("git", c.FileName));
        Assert.Equal(new[] { "add", "--", ".agents" }, fake.Calls[0].Args);
        Assert.Equal(new[] { "commit", "-m", Cli.AgentsSubmodulePublisher.GitlinkPointerMessage }, fake.Calls[1].Args);
        Assert.Equal(new[] { "push" }, fake.Calls[2].Args);
    }

    [Fact]
    public async Task RecordParentGitlinkAsync_PushFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] == "push"
                ? FakeProcessRunner.Fail("parent push rejected")
                : FakeProcessRunner.Ok()
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).RecordParentGitlinkAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordParentGitlinkAsync_PushFailure_WhenUpstreamAlreadyHasHead_IsTreatedAsPushed()
    {
        const string head = "24803b3509216ff9ea0d1d0f8f45f7e19149f8c4";
        // Full-args dispatch: `rev-parse HEAD` and `rev-parse @{u}` are distinct stubs (see the
        // submodule-path twin of this test for why).
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args switch
            {
                ["push"] => FakeProcessRunner.Fail(
                    "cannot lock ref 'refs/heads/main': is at " + head + " but expected a75de6f"),
                ["fetch", ..] => FakeProcessRunner.Ok(),
                ["rev-parse", "HEAD"] => FakeProcessRunner.Ok(head),
                ["rev-parse", "@{u}"] => FakeProcessRunner.Ok(head),
                _ => FakeProcessRunner.Ok()
            }
        };

        await New(fake).RecordParentGitlinkAsync(CancellationToken.None); // must not throw

        Assert.Contains(fake.Calls, c => c.Args.SequenceEqual(new[] { "fetch", "--quiet" }));
        Assert.Contains(fake.Calls, c => c.Args.SequenceEqual(new[] { "rev-parse", "HEAD" }));
        Assert.Contains(fake.Calls, c => c.Args.SequenceEqual(new[] { "rev-parse", "@{u}" }));
    }

    [Fact]
    public async Task RecordParentGitlinkAsync_PushFailure_WhenUpstreamIsAtADifferentSha_Throws()
    {
        // Both shas resolve (non-empty) but DIFFER: the parent pointer commit did NOT land upstream, and
        // this path has no stranded-commit recovery — treating it as pushed would silently strand it.
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args switch
            {
                ["push"] => FakeProcessRunner.Fail("parent push rejected"),
                ["fetch", ..] => FakeProcessRunner.Ok(),
                ["rev-parse", "HEAD"] => FakeProcessRunner.Ok("24803b3509216ff9ea0d1d0f8f45f7e19149f8c4"),
                ["rev-parse", "@{u}"] => FakeProcessRunner.Ok("a75de6f0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<Cli.PlanStepException>(
            () => New(fake).RecordParentGitlinkAsync(CancellationToken.None));
    }
}

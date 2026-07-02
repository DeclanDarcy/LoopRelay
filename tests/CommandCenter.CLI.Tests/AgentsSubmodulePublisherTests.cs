using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class AgentsSubmodulePublisherTests
{
    private const string Message = "Orchestration loop: context update before execution";

    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    private static bool IsSubmodule(string workingDirectory) =>
        workingDirectory.Replace('\\', '/').EndsWith("/.agents", StringComparison.Ordinal);

    private static AgentsSubmodulePublisher New(FakeProcessRunner fake) =>
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
    public async Task CleanAndUpToDate_ReturnsFalse_AndCommitsNothing()
    {
        var fake = Runner(status: string.Empty); // rev-list falls through to Ok("") -> not ahead of upstream

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.False(committed);
        // Nothing to commit and nothing to recover -> no add/commit/push.
        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "add" or "commit" or "push");
        Assert.Contains(fake.Calls, c => c.Args[0] == "status");
    }

    [Fact]
    public async Task CleanButAheadOfUpstream_PushesStrandedCommit_WithoutCommitting()
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

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.False(committed); // no NEW commit was made
        // The stranded commit is pushed (inside the submodule), without staging or committing again.
        Assert.Contains(fake.Calls, c => c.Args[0] == "push" && IsSubmodule(c.WorkingDirectory));
        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "add" or "commit");
    }

    [Fact]
    public async Task DirtyOnBranch_CommitsAndPushes_AtSubmoduleWorkingDirectory()
    {
        var fake = Runner(status: " M decisions/decisions.md");

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.All(fake.Calls, c => Assert.Equal("git", c.FileName));

        // The submodule commit+push runs inside the `.agents` working directory, in this exact order. (The
        // parent repo is probed for its gitlink afterwards — clean here — so those calls are asserted separately.)
        var submoduleCalls = fake.Calls.Where(c => IsSubmodule(c.WorkingDirectory)).ToList();
        Assert.Equal(new[] { "status", "--porcelain" }, submoduleCalls[0].Args);
        Assert.Equal(new[] { "branch", "--show-current" }, submoduleCalls[1].Args);
        Assert.Equal(new[] { "add", "-A" }, submoduleCalls[2].Args);
        Assert.Equal(new[] { "commit", "-m", Message }, submoduleCalls[3].Args);
        Assert.Equal(new[] { "push" }, submoduleCalls[4].Args);
    }

    [Fact]
    public async Task DirtyOnBranch_AlsoRecordsAndPushesParentGitlink_StagingOnlyAgents()
    {
        // A fresh submodule commit (submodule dirty) advances the pointer, so the publisher records it in the
        // parent — WITHOUT probing `git status`; the commit itself is proof the pointer moved.
        var fake = Runner(status: " M decisions/decisions.md");

        await New(fake).PublishAsync(Message, CancellationToken.None);

        // The parent-repo half of the publish runs in the repo root (never the submodule), stages ONLY the
        // `.agents` gitlink (not `-A` — the parent's real working-tree changes belong to CommitGate), and pushes.
        // No status call appears: the reconcile is gated on `committed`, not on a probe.
        var parentCalls = fake.Calls.Where(c => !IsSubmodule(c.WorkingDirectory)).ToList();
        Assert.All(parentCalls, c => Assert.Equal("/repo", c.WorkingDirectory.Replace('\\', '/')));
        Assert.DoesNotContain(parentCalls, c => c.Args[0] == "status");
        Assert.Equal(new[] { "add", "--", ".agents" }, parentCalls[0].Args);
        Assert.Equal(new[] { "commit", "-m", AgentsSubmodulePublisher.GitlinkPointerMessage }, parentCalls[1].Args);
        Assert.Equal(new[] { "push" }, parentCalls[2].Args);
    }

    [Fact]
    public async Task CleanSubmodule_RecordsNoParentGitlink()
    {
        // No submodule commit (clean, up to date) => the pointer never moved => no parent-repo git at all.
        var fake = Runner(status: string.Empty);

        await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.DoesNotContain(fake.Calls, c => !IsSubmodule(c.WorkingDirectory));
    }

    [Fact]
    public async Task ParentGitlinkPushFailure_Throws()
    {
        // Submodule publishes fine, but pushing the parent gitlink fails — strict push, same as the submodule.
        var fake = new FakeProcessRunner
        {
            Handler = (dir, args) => (args[0], IsSubmodule(dir)) switch
            {
                ("status", _) => FakeProcessRunner.Ok(" M decisions/decisions.md"),
                ("branch", _) => FakeProcessRunner.Ok("main"),
                ("push", false) => FakeProcessRunner.Fail("parent push rejected"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<LoopStepException>(
            () => New(fake).PublishAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task ParentGitlinkPushFailure_WhenUpstreamAlreadyHasHead_IsTreatedAsPushed()
    {
        const string head = "24803b3509216ff9ea0d1d0f8f45f7e19149f8c4";
        var fake = new FakeProcessRunner
        {
            Handler = (dir, args) => (args[0], IsSubmodule(dir)) switch
            {
                ("status", _) => FakeProcessRunner.Ok(" M decisions/decisions.md"),
                ("branch", _) => FakeProcessRunner.Ok("main"),
                ("push", false) => FakeProcessRunner.Fail(
                    "cannot lock ref 'refs/heads/main': is at " + head + " but expected a75de6f"),
                ("fetch", false) => FakeProcessRunner.Ok(),
                ("rev-parse", false) when args[1] == "HEAD" => FakeProcessRunner.Ok(head),
                ("rev-parse", false) when args[1] == "@{u}" => FakeProcessRunner.Ok(head),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.Contains(fake.Calls, c => !IsSubmodule(c.WorkingDirectory) && c.Args.SequenceEqual(new[] { "fetch", "--quiet" }));
        Assert.Contains(fake.Calls, c => !IsSubmodule(c.WorkingDirectory) && c.Args.SequenceEqual(new[] { "rev-parse", "HEAD" }));
        Assert.Contains(fake.Calls, c => !IsSubmodule(c.WorkingDirectory) && c.Args.SequenceEqual(new[] { "rev-parse", "@{u}" }));
    }

    [Fact]
    public async Task CommitMessage_IsPassedThrough()
    {
        var fake = Runner(status: " M handoffs/handoff.md");

        await New(fake).PublishAsync(AgentsSubmodulePublisher.ExecutionHandoffMessage, CancellationToken.None);

        // The submodule commit carries the passed-through message (the parent gitlink commit is asserted
        // separately and carries GitlinkPointerMessage, so scope to the submodule working directory here).
        var commit = fake.Calls.Single(c => c.Args[0] == "commit" && IsSubmodule(c.WorkingDirectory));
        Assert.Equal(new[] { "commit", "-m", AgentsSubmodulePublisher.ExecutionHandoffMessage }, commit.Args);
    }

    [Fact]
    public async Task DetachedHead_Throws_WithoutCommittingOrPushing()
    {
        // Dirty tree but detached HEAD (blank `branch --show-current`) cannot be pushed.
        var fake = Runner(status: " M decisions/decisions.md", branch: string.Empty);

        await Assert.ThrowsAsync<LoopStepException>(
            () => New(fake).PublishAsync(Message, CancellationToken.None));

        Assert.DoesNotContain(fake.Calls, c => c.Args[0] is "commit" or "push");
    }

    [Fact]
    public async Task PushFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M decisions/decisions.md"),
                "branch" => FakeProcessRunner.Ok("main"),
                "push" => FakeProcessRunner.Fail("push rejected"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<LoopStepException>(
            () => New(fake).PublishAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task SubmodulePushFailure_WhenRetrySucceeds_ContinuesToParentGitlink()
    {
        int submodulePushes = 0;
        var fake = new FakeProcessRunner
        {
            Handler = (dir, args) => (args[0], IsSubmodule(dir)) switch
            {
                ("status", _) => FakeProcessRunner.Ok(" M handoffs/handoff.md"),
                ("branch", _) => FakeProcessRunner.Ok("main"),
                ("push", true) => ++submodulePushes == 1
                    ? FakeProcessRunner.Fail("Recv failure: Connection was reset")
                    : FakeProcessRunner.Ok(),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.Equal(2, submodulePushes);
        Assert.Contains(fake.Calls, c =>
            !IsSubmodule(c.WorkingDirectory) &&
            c.Args.SequenceEqual(new[] { "commit", "-m", AgentsSubmodulePublisher.GitlinkPointerMessage }));
    }

    [Fact]
    public async Task SubmodulePushFailure_WhenUpstreamAlreadyHasHead_IsTreatedAsPushed()
    {
        const string head = "918eca5cc5762e21c267303838fa2dcec1461963";
        var fake = new FakeProcessRunner
        {
            Handler = (dir, args) => (args[0], IsSubmodule(dir)) switch
            {
                ("status", _) => FakeProcessRunner.Ok(" M handoffs/handoff.md"),
                ("branch", _) => FakeProcessRunner.Ok("main"),
                ("push", true) => FakeProcessRunner.Fail("Recv failure: Connection was reset"),
                ("fetch", true) => FakeProcessRunner.Ok(),
                ("rev-parse", true) when args[1] == "HEAD" => FakeProcessRunner.Ok(head),
                ("rev-parse", true) when args[1] == "@{u}" => FakeProcessRunner.Ok(head),
                _ => FakeProcessRunner.Ok()
            }
        };

        bool committed = await New(fake).PublishAsync(Message, CancellationToken.None);

        Assert.True(committed);
        Assert.Single(fake.Calls, c => IsSubmodule(c.WorkingDirectory) && c.Args.SequenceEqual(new[] { "push" }));
        Assert.Contains(fake.Calls, c =>
            !IsSubmodule(c.WorkingDirectory) &&
            c.Args.SequenceEqual(new[] { "commit", "-m", AgentsSubmodulePublisher.GitlinkPointerMessage }));
    }

    [Fact]
    public async Task CommitFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] switch
            {
                "status" => FakeProcessRunner.Ok(" M decisions/decisions.md"),
                "branch" => FakeProcessRunner.Ok("main"),
                "commit" => FakeProcessRunner.Fail("nothing to commit"),
                _ => FakeProcessRunner.Ok()
            }
        };

        await Assert.ThrowsAsync<LoopStepException>(
            () => New(fake).PublishAsync(Message, CancellationToken.None));
    }

    [Fact]
    public async Task StatusFailure_Throws()
    {
        var fake = new FakeProcessRunner
        {
            Handler = (_, args) => args[0] == "status"
                ? FakeProcessRunner.Fail("not a git repository")
                : FakeProcessRunner.Ok()
        };

        await Assert.ThrowsAsync<LoopStepException>(
            () => New(fake).PublishAsync(Message, CancellationToken.None));
    }
}

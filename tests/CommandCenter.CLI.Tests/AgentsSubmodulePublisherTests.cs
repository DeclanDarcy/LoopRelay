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
        // Every git call runs inside the `.agents` submodule working directory.
        Assert.All(fake.Calls, c => Assert.True(IsSubmodule(c.WorkingDirectory), c.WorkingDirectory));

        Assert.Equal(new[] { "status", "--porcelain" }, fake.Calls[0].Args);
        Assert.Equal(new[] { "branch", "--show-current" }, fake.Calls[1].Args);
        Assert.Equal(new[] { "add", "-A" }, fake.Calls[2].Args);
        Assert.Equal(new[] { "commit", "-m", Message }, fake.Calls[3].Args);
        Assert.Equal(new[] { "push" }, fake.Calls[4].Args);
    }

    [Fact]
    public async Task CommitMessage_IsPassedThrough()
    {
        var fake = Runner(status: " M handoffs/handoff.md");

        await New(fake).PublishAsync(AgentsSubmodulePublisher.ExecutionHandoffMessage, CancellationToken.None);

        var commit = fake.Calls.Single(c => c.Args[0] == "commit");
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

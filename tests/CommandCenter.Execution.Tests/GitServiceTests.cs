using CommandCenter.Execution;
using CommandCenter.Core.Repositories;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Execution.Services;

namespace CommandCenter.Execution.Tests;

[Collection("ProcessEnvironment")]
public sealed class GitServiceTests
{
    [Fact]
    public async Task StatusCapturesBranchDivergenceAndChangedPathBuckets()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0, StandardOutput = "feature/context\n" },
            new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "## feature/context...origin/feature/context [ahead 2, behind 1]\0M  staged.txt\0A  added.txt\0 M modified.txt\0 D deleted.txt\0R  renamed-new.txt\0renamed-old.txt\0?? untracked.txt\0"
            });
        var service = new GitService(runner);

        RepositoryGitStatus status = await service.GetStatusAsync(new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = CreateTemporaryDirectory()
        });

        Assert.Equal("feature/context", status.Branch);
        Assert.Equal(2, status.AheadCount);
        Assert.Equal(1, status.BehindCount);
        Assert.False(status.DirtyState.IsClean);
        Assert.Contains("staged.txt", status.DirtyState.StagedPaths);
        Assert.Contains("added.txt", status.DirtyState.AddedPaths);
        Assert.Contains("added.txt", status.DirtyState.StagedPaths);
        Assert.Contains("modified.txt", status.DirtyState.ModifiedPaths);
        Assert.Contains("deleted.txt", status.DirtyState.DeletedPaths);
        Assert.Contains("renamed-new.txt", status.DirtyState.RenamedPaths);
        Assert.Contains("untracked.txt", status.DirtyState.UntrackedPaths);
    }

    [Fact]
    public async Task SnapshotUsesParsedStatus()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0, StandardOutput = "feature/context\n" },
            new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "## feature/context...origin/feature/context [ahead 1]\0 M modified.txt\0"
            });
        var service = new GitService(runner);

        RepositorySnapshot snapshot = await service.GetSnapshotAsync(new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = CreateTemporaryDirectory()
        });

        Assert.Equal("feature/context", snapshot.Branch);
        Assert.False(snapshot.DirtyState.IsClean);
        Assert.Contains("modified.txt", snapshot.DirtyState.ModifiedPaths);
    }

    [Fact]
    public async Task PrepareCommitBuildsDeterministicMessageScopeAndOrigins()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0, StandardOutput = "main\n" },
            new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "## main\0 M src/pre-existing.cs\0?? src/new.cs\0"
            });
        var service = new GitService(runner);
        var session = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = Guid.NewGuid(),
            RepositoryPath = CreateTemporaryDirectory(),
            RepositorySnapshot = new RepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState
                {
                    ModifiedPaths = ["src/pre-existing.cs"],
                    IsClean = false
                },
                CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        };

        CommitPreparation preparation = await service.PrepareCommitAsync(
            new Repository
            {
                Id = session.RepositoryId,
                Name = "Repo",
                Path = session.RepositoryPath
            },
            session);

        Assert.Equal("Execution changes\n\n- 2 files changed", preparation.ProposedMessage);
        Assert.Equal(session.Id, preparation.SessionId);
        Assert.Equal(session.RepositoryId, preparation.RepositoryId);
        Assert.NotEqual(Guid.Empty, preparation.Id);
        Assert.False(string.IsNullOrWhiteSpace(preparation.StatusSnapshot.Id));
        Assert.True(preparation.HasPreExistingChanges);
        Assert.Equal(2, preparation.ScopeItems.Count);
        Assert.All(preparation.ScopeItems, item => Assert.True(item.IsSelected));

        CommitScopeItem preExisting = Assert.Single(preparation.ScopeItems, item => item.Path == "src/pre-existing.cs");
        Assert.Equal(CommitChangeType.Modified, preExisting.ChangeType);
        Assert.Equal(CommitChangeOrigin.PreExisting, preExisting.Origin);
        Assert.Equal(
            "Path was dirty in the launch-time repository snapshot captured before execution.",
            preExisting.OriginBasis);

        CommitScopeItem generated = Assert.Single(preparation.ScopeItems, item => item.Path == "src/new.cs");
        Assert.Equal(CommitChangeType.Untracked, generated.ChangeType);
        Assert.Equal(CommitChangeOrigin.ExecutionGenerated, generated.Origin);
        Assert.Equal(
            "Path was absent from the launch-time dirty snapshot and appeared after execution.",
            generated.OriginBasis);
    }

    [Fact]
    public async Task CommitStagesOnlySelectedPathsAndStoresHeadSha()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0 },
            new ProcessRunResult { ExitCode = 0 },
            new ProcessRunResult { ExitCode = 0, StandardOutput = "abc123\n" });
        var service = new GitService(runner);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = CreateTemporaryDirectory()
        };

        CommitResult result = await service.CommitAsync(
            repository,
            "Reviewed commit",
            ["src/selected.cs", "docs/notes.md"],
            "snapshot");

        Assert.Equal("abc123", result.CommitSha);
        Assert.Equal("Reviewed commit", result.CommitMessage);
        Assert.Equal("snapshot", result.PreparationSnapshotId);
        Assert.Equal(["src/selected.cs", "docs/notes.md"], result.SelectedPaths);
        Assert.Collection(
            runner.Calls,
            call =>
            {
                Assert.Equal("git", call.FileName);
                Assert.Equal(["add", "-A", "--", "src/selected.cs", "docs/notes.md"], call.Arguments);
            },
            call => Assert.Equal(["commit", "-m", "Reviewed commit"], call.Arguments),
            call => Assert.Equal(["rev-parse", "HEAD"], call.Arguments));
    }

    [Fact]
    public async Task PushRunsGitPushAndRefreshesStatus()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0 },
            new ProcessRunResult { ExitCode = 0, StandardOutput = "main\n" },
            new ProcessRunResult { ExitCode = 0, StandardOutput = "## main...origin/main\0" });
        var service = new GitService(runner);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = CreateTemporaryDirectory()
        };

        PushResult result = await service.PushAsync(repository, "abc123");

        Assert.Equal("abc123", result.PushedCommitSha);
        Assert.Equal("main", result.BranchName);
        Assert.True(result.PushAttemptedAt <= result.PushedAt);
        Assert.Collection(
            runner.Calls,
            call => Assert.Equal(["push"], call.Arguments),
            call => Assert.Equal(["branch", "--show-current"], call.Arguments),
            call => Assert.Equal(["status", "--porcelain=v1", "--branch", "-z"], call.Arguments));
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeProcessRunner(params ProcessRunResult[] results) : IProcessRunner
    {
        private int index;

        public List<ProcessCall> Calls { get; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory)
        {
            Calls.Add(new ProcessCall(fileName, arguments.ToArray(), workingDirectory));
            return Task.FromResult(results[index++]);
        }

        public Task<ProcessStartResult> StartAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            string? standardInput = null,
            Func<string, Task>? onStandardOutput = null,
            Func<string, Task>? onStandardError = null,
            Func<int?, Task>? onExit = null)
        {
            throw new NotSupportedException();
        }

        public Task<IAgentProcess> StartInteractiveAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record ProcessCall(
        string FileName,
        IReadOnlyList<string> Arguments,
        string WorkingDirectory);
}

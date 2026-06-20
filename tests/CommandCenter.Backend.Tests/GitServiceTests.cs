using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class GitServiceTests
{
    [Fact]
    public async Task SnapshotCapturesBranchAndChangedPathBuckets()
    {
        var runner = new FakeProcessRunner(
            new ProcessRunResult { ExitCode = 0, StandardOutput = "feature/context\n" },
            new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "M  staged.txt\0 M modified.txt\0 D deleted.txt\0R  renamed-new.txt\0renamed-old.txt\0?? untracked.txt\0"
            });
        var service = new GitService(runner);

        var snapshot = await service.GetSnapshotAsync(new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = CreateTemporaryDirectory()
        });

        Assert.Equal("feature/context", snapshot.Branch);
        Assert.False(snapshot.DirtyState.IsClean);
        Assert.Contains("staged.txt", snapshot.DirtyState.StagedPaths);
        Assert.Contains("modified.txt", snapshot.DirtyState.ModifiedPaths);
        Assert.Contains("deleted.txt", snapshot.DirtyState.DeletedPaths);
        Assert.Contains("renamed-new.txt", snapshot.DirtyState.RenamedPaths);
        Assert.Contains("untracked.txt", snapshot.DirtyState.UntrackedPaths);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeProcessRunner(params ProcessRunResult[] results) : IProcessRunner
    {
        private int index;

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory)
        {
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
    }
}

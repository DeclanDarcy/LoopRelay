using System.Net;
using System.Net.Http.Json;
using CommandCenter.Backend;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

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

        var status = await service.GetStatusAsync(new Repository
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

        var snapshot = await service.GetSnapshotAsync(new Repository
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
    public async Task StatusEndpointReturnsRepositoryGitStatus()
    {
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());

        await using var app = Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IRepositoryService>(repositoryService);
                services.AddSingleton<IGitService>(new FakeGitService());
            });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var response = await client.GetAsync(app.Urls.Single() + $"/api/repositories/{repository.Id}/git/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<RepositoryGitStatus>();
        Assert.NotNull(status);
        Assert.Equal("main", status.Branch);
        Assert.Equal(1, status.AheadCount);
        Assert.Contains("src/changed.cs", status.DirtyState.ModifiedPaths);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateGitRepositoryDirectory()
    {
        var directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private sealed class FakeGitService : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState
                {
                    ModifiedPaths = ["src/changed.cs"],
                    IsClean = false
                },
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                AheadCount = 1,
                BehindCount = 0,
                DirtyState = new RepositoryDirtyState
                {
                    ModifiedPaths = ["src/changed.cs"],
                    IsClean = false
                },
                CapturedAt = DateTimeOffset.UtcNow
            });
        }
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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            MilestonePath = ".agents/milestones/m6-git-lifecycle.md",
            RepositorySnapshot = new ExecutionRepositorySnapshot
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

        var preparation = await service.PrepareCommitAsync(
            new Repository
            {
                Id = session.RepositoryId,
                Name = "Repo",
                Path = session.RepositoryPath
            },
            session);

        Assert.Equal("m6-git-lifecycle\n\n- 2 files changed", preparation.ProposedMessage);
        Assert.Equal(session.Id, preparation.SessionId);
        Assert.Equal(session.RepositoryId, preparation.RepositoryId);
        Assert.NotEqual(Guid.Empty, preparation.Id);
        Assert.False(string.IsNullOrWhiteSpace(preparation.StatusSnapshot.Id));
        Assert.True(preparation.HasPreExistingChanges);
        Assert.Equal(2, preparation.ScopeItems.Count);
        Assert.All(preparation.ScopeItems, item => Assert.True(item.IsSelected));

        var preExisting = Assert.Single(preparation.ScopeItems, item => item.Path == "src/pre-existing.cs");
        Assert.Equal(CommitChangeType.Modified, preExisting.ChangeType);
        Assert.Equal(CommitChangeOrigin.PreExisting, preExisting.Origin);

        var generated = Assert.Single(preparation.ScopeItems, item => item.Path == "src/new.cs");
        Assert.Equal(CommitChangeType.Untracked, generated.ChangeType);
        Assert.Equal(CommitChangeOrigin.ExecutionGenerated, generated.Origin);
    }

    [Fact]
    public async Task PrepareCommitEndpointPersistsPreparationOnSession()
    {
        var storePath = Path.Combine(CreateTemporaryDirectory(), "execution-sessions.json");
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());
        var session = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            RepositoryPath = repository.Path,
            MilestonePath = ".agents/milestones/m6-git-lifecycle.md",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AcceptedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingCommit,
            ProviderName = "fake",
            RepositorySnapshot = new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState
                {
                    ModifiedPaths = ["src/changed.cs"],
                    IsClean = false
                },
                CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        };
        var store = new FileSystemExecutionSessionStore(storePath);
        await store.SaveAsync([session]);

        await using var app = Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IRepositoryService>(repositoryService);
                services.AddSingleton<IExecutionSessionStore>(new FileSystemExecutionSessionStore(storePath));
                services.AddSingleton<IGitService>(new FakeGitService());
            });
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        var response = await client.PostAsync(
            app.Urls.Single() + $"/api/execution-sessions/{session.Id}/git/prepare-commit",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var preparation = await response.Content.ReadFromJsonAsync<CommitPreparation>(jsonOptions);
        Assert.NotNull(preparation);
        Assert.Equal("snapshot", preparation.StatusSnapshot.Id);

        var reloadedSession = Assert.Single(await new FileSystemExecutionSessionStore(storePath).LoadAsync());
        Assert.NotNull(reloadedSession.CommitPreparation);
        Assert.Equal("snapshot", reloadedSession.CommitPreparation.StatusSnapshot.Id);
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

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            return Task.FromResult(new CommitPreparation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RepositoryId = repository.Id,
                RepositoryPath = repository.Path,
                ProposedMessage = "m6-git-lifecycle\n\n- 1 file changed",
                ScopeItems =
                [
                    new CommitScopeItem
                    {
                        Path = "src/changed.cs",
                        ChangeType = CommitChangeType.Modified,
                        Origin = CommitChangeOrigin.ExecutionGenerated,
                        IsSelected = true
                    }
                ],
                StatusSnapshot = new CommitStatusSnapshot
                {
                    Id = "snapshot",
                    Branch = "main",
                    DirtyState = new RepositoryDirtyState
                    {
                        ModifiedPaths = ["src/changed.cs"],
                        IsClean = false
                    },
                    CapturedAt = DateTimeOffset.UtcNow
                },
                GeneratedAt = DateTimeOffset.UtcNow
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

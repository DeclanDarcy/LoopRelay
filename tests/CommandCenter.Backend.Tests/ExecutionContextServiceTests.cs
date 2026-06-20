using System.Net;
using System.Net.Http.Json;
using CommandCenter.Backend;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionContextServiceTests
{
    [Fact]
    public async Task ContextBuildsWithPlanAndSelectedMilestone()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
        Assert.Contains(context.Artifacts, artifact => artifact.Role == "Plan");
        Assert.Contains(context.Artifacts, artifact => artifact.Role == "Milestone");
        Assert.NotNull(context.RepositorySnapshot);
        Assert.True(context.RepositorySnapshot.DirtyState.IsClean);
    }

    [Fact]
    public async Task MissingOptionalHandoffAndDecisionsSucceedAndAreReported()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.Contains(".agents/handoffs/handoff.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Contains(".agents/decisions/decisions.md", context.Diagnostics.MissingOptionalArtifacts);
    }

    [Fact]
    public async Task MissingPlanFailsValidation()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("plan", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task MissingSelectedMilestoneFailsValidation()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/missing.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("missing.md", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task NonMilestonePathFailsValidation()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/plan.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains(".agents/milestones", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task WarningThresholdProducesWarningWithoutHardFailure()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 100 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.True(context.Diagnostics.WarningThresholdExceeded);
        Assert.False(context.Diagnostics.HardLimitExceeded);
        Assert.False(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task HardLimitProducesLaunchBlockingDiagnostics()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 260 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.True(context.Diagnostics.HardLimitExceeded);
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task DirtyRepositoryStateIsCapturedWithoutBlockingPreview()
    {
        var dirtyState = new RepositoryDirtyState
        {
            ModifiedPaths = ["src/file.cs"],
            IsClean = false
        };
        var harness = await CreateHarnessAsync(dirtyState);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
        Assert.False(context.RepositorySnapshot!.DirtyState.IsClean);
        Assert.Contains("src/file.cs", context.RepositorySnapshot.DirtyState.ModifiedPaths);
    }

    [Fact]
    public async Task GitSnapshotFailureIsReportedAsValidationError()
    {
        var harness = await CreateHarnessAsync(gitFailure: "git unavailable");
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        var context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("git unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task ContextEndpointReturnsDiagnostics()
    {
        var configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        var previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);

        try
        {
            var repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            var repository = await repositoryService.RegisterAsync(repositoryPath);
            await WriteAsync(repository, ".agents/plan.md", "plan");
            await WriteAsync(repository, ".agents/milestones/m1.md", "milestone");

            await using var app = Program.CreateApp(
                [],
                services => services.AddSingleton<IGitService>(new FakeGitService(null, null)));
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            var response = await client.GetAsync(
                app.Urls.Single() +
                $"/api/repositories/{repository.Id}/execution/context?milestonePath=.agents/milestones/m1.md");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var context = await response.Content.ReadFromJsonAsync<CommandCenter.Backend.Execution.ExecutionContext>();
            Assert.NotNull(context);
            Assert.Contains(context.Artifacts, artifact => artifact.RelativePath == ".agents/plan.md");
            Assert.NotNull(context.RepositorySnapshot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        RepositoryDirtyState? dirtyState = null,
        string? gitFailure = null)
    {
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());
        var artifactStore = new FileSystemArtifactStore();
        var contextService = new ExecutionContextService(
            repositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            new FakeGitService(dirtyState, gitFailure));

        return new Harness(repository, contextService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        var path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static string CreateGitRepositoryDirectory()
    {
        var directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record Harness(Repository Repository, ExecutionContextService ContextService);

    private sealed class FakeGitService(RepositoryDirtyState? dirtyState, string? failure) : IGitService
    {
        public Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            throw new NotSupportedException();
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Execution;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionContextServiceTests
{
    [Fact]
    public async Task ContextBuildsWithPlanAndSelectedMilestone()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
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
    public async Task OperationalContextIsIncludedWhenPresent()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "operational context");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        ExecutionContextArtifact artifact = Assert.Single(
            context.Artifacts,
            artifact => artifact.Role == "OperationalContext");
        Assert.Equal(".agents/operational_context.md", artifact.RelativePath);
        Assert.Equal("operational context", artifact.Content);
        Assert.DoesNotContain(".agents/operational_context.md", context.Diagnostics.MissingOptionalArtifacts);
    }

    [Fact]
    public async Task EmptyOperationalContextIsAllowed()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", string.Empty);
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        ExecutionContextArtifact artifact = Assert.Single(
            context.Artifacts,
            artifact => artifact.Role == "OperationalContext");
        Assert.Equal(0, artifact.ByteCount);
        Assert.Equal(0, artifact.CharacterCount);
        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task MissingOptionalArtifactsSucceedAndAreReported()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.Contains(".agents/operational_context.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Contains(".agents/handoffs/handoff.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Contains(".agents/decisions/decisions.md", context.Diagnostics.MissingOptionalArtifacts);
    }

    [Fact]
    public async Task MissingPlanFailsValidation()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("plan", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task MissingSelectedMilestoneFailsValidation()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/missing.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("missing.md", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task NonMilestonePathFailsValidation()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/plan.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains(".agents/milestones", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task WarningThresholdProducesWarningWithoutHardFailure()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 100 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.True(context.Diagnostics.WarningThresholdExceeded);
        Assert.False(context.Diagnostics.HardLimitExceeded);
        Assert.False(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task HardLimitProducesLaunchBlockingDiagnostics()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 260 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.True(context.Diagnostics.HardLimitExceeded);
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task OperationalContextContributesToSizeDiagnostics()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", new string('c', 100 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        ExecutionContextArtifactDiagnostic diagnostic = Assert.Single(
            context.Diagnostics.ArtifactDiagnostics,
            diagnostic => diagnostic.Role == "OperationalContext");
        Assert.Equal(".agents/operational_context.md", diagnostic.RelativePath);
        Assert.Equal(100 * 1024, diagnostic.ByteCount);
        Assert.True(diagnostic.WarningThresholdExceeded);
        Assert.False(diagnostic.HardLimitExceeded);
        Assert.True(context.Diagnostics.WarningThresholdExceeded);
        Assert.False(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task OversizedOperationalContextBlocksLaunch()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", new string('c', 260 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        ExecutionContextArtifactDiagnostic diagnostic = Assert.Single(
            context.Diagnostics.ArtifactDiagnostics,
            diagnostic => diagnostic.Role == "OperationalContext");
        Assert.True(diagnostic.HardLimitExceeded);
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
        Harness harness = await CreateHarnessAsync(dirtyState);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
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
        Harness harness = await CreateHarnessAsync(gitFailure: "git unavailable");
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("git unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task DecisionProjectionIsIncludedWhenProjectionServiceIsAvailable()
    {
        var projection = new ExecutionDecisionProjection(
            Guid.Empty,
            DateTimeOffset.UtcNow,
            [],
            [
                new ExecutionDirective(
                    "EDIR-0001",
                    "DEC-0001",
                    "Workflow policy",
                    "Use repository artifacts",
                    DecisionClassification.Operational,
                    ExecutionProjectionKind.RepositoryConvention,
                    [])
            ],
            [],
            []);
        Harness harness = await CreateHarnessAsync(decisionProjection: projection);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id,
            ".agents/milestones/m1.md");

        Assert.NotNull(context.DecisionProjection);
        Assert.Equal("DEC-0001", Assert.Single(context.DecisionProjection.Directives).DecisionId);
        Assert.Empty(context.Diagnostics.ValidationErrors);
    }

    [Fact]
    public async Task ContextEndpointReturnsDiagnostics()
    {
        string configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        string? previousConfigurationPath = Environment.GetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH");
        Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", configurationPath);

        try
        {
            string repositoryPath = CreateGitRepositoryDirectory();
            var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
            Repository repository = await repositoryService.RegisterAsync(repositoryPath);
            await WriteAsync(repository, ".agents/plan.md", "plan");
            await WriteAsync(repository, ".agents/operational_context.md", "context");
            await WriteAsync(repository, ".agents/milestones/m1.md", "milestone");

            await using WebApplication app = Program.CreateApp(
                [],
                services => services.AddSingleton<IGitService>(new FakeGitService(null, null)));
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();

            using var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(
                app.Urls.Single() +
                $"/api/repositories/{repository.Id}/execution/context?milestonePath=.agents/milestones/m1.md");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var context = await response.Content.ReadFromJsonAsync<Execution.Models.ExecutionContext>();
            Assert.NotNull(context);
            Assert.Contains(context.Artifacts, artifact => artifact.RelativePath == ".agents/plan.md");
            Assert.Contains(context.Artifacts, artifact => artifact.RelativePath == ".agents/operational_context.md");
            Assert.Contains(context.Diagnostics.ArtifactDiagnostics, diagnostic => diagnostic.RelativePath == ".agents/operational_context.md");
            Assert.NotNull(context.RepositorySnapshot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND_CENTER_CONFIGURATION_PATH", previousConfigurationPath);
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        RepositoryDirtyState? dirtyState = null,
        string? gitFailure = null,
        ExecutionDecisionProjection? decisionProjection = null)
    {
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        Repository repository = await repositoryService.RegisterAsync(CreateGitRepositoryDirectory());
        var artifactStore = new FileSystemArtifactStore();
        var contextService = new ExecutionContextService(
            repositoryService,
            new ArtifactService(artifactStore),
            new PlanningService(artifactStore),
            new FakeGitService(dirtyState, gitFailure),
            decisionProjection is null ? null : new FakeDecisionProjectionService(decisionProjection));

        return new Harness(repository, contextService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static string CreateGitRepositoryDirectory()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            throw new NotSupportedException();
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            throw new NotSupportedException();
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDecisionProjectionService(ExecutionDecisionProjection projection) : IDecisionProjectionService
    {
        public Task<ExecutionDecisionProjection> BuildExecutionProjectionAsync(
            Guid repositoryId,
            string? executionRequest = null,
            string? milestoneContent = null)
        {
            return Task.FromResult(projection with { RepositoryId = repositoryId });
        }
    }
}

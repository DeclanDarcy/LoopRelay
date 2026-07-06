using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Configuration;
using LoopRelay.Execution;
using LoopRelay.Core.Planning;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Execution.Abstractions;
using LoopRelay.Execution.Models;
using LoopRelay.Execution.Services;

namespace LoopRelay.Execution.Tests;

[Collection("ProcessEnvironment")]
public sealed class ImplementationExecutionContextServiceTests
{
    [Fact]
    public async Task ContextBuildsWithPlan()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
        Assert.Contains(context.Artifacts, artifact => artifact.Role == "Plan");
        Assert.NotNull(context.Snapshot);
        Assert.True(context.Snapshot.DirtyState.IsClean);
    }

    [Fact]
    public async Task OperationalContextIsIncludedWhenPresent()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "operational context");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        LoopRelay.Core.Artifacts.LoadedArtifact artifact = Assert.Single(
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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        LoopRelay.Core.Artifacts.LoadedArtifact artifact = Assert.Single(
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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.Contains(".agents/operational_context.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Contains(".agents/handoffs/handoff.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Contains(".agents/decisions/decisions.md", context.Diagnostics.MissingOptionalArtifacts);
    }

    [Fact]
    public async Task DecisionsArtifactIsIncludedWhenPresent()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", "DEC-0001: raw decision text");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        LoopRelay.Core.Artifacts.LoadedArtifact artifact = Assert.Single(
            context.Artifacts,
            artifact => artifact.Role == "Decisions");
        Assert.Equal(".agents/decisions/decisions.md", artifact.RelativePath);
        Assert.Equal("DEC-0001: raw decision text", artifact.Content);
        Assert.DoesNotContain(".agents/decisions/decisions.md", context.Diagnostics.MissingOptionalArtifacts);
        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task MissingPlanFailsValidation()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("plan", StringComparison.OrdinalIgnoreCase));
        Assert.True(context.Diagnostics.LaunchBlocked);
    }

    [Fact]
    public async Task WarningThresholdProducesWarningWithoutHardFailure()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", new string('a', 100 * 1024));
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        ImplementationExecutionContextArtifactDiagnostic diagnostic = Assert.Single(
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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        ImplementationExecutionContextArtifactDiagnostic diagnostic = Assert.Single(
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

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        Assert.Empty(context.Diagnostics.ValidationErrors);
        Assert.False(context.Diagnostics.LaunchBlocked);
        Assert.False(context.Snapshot!.DirtyState.IsClean);
        Assert.Contains("src/file.cs", context.Snapshot.DirtyState.ModifiedPaths);
    }

    [Fact]
    public async Task GitSnapshotFailureIsReportedAsValidationError()
    {
        Harness harness = await CreateHarnessAsync(gitFailure: "git unavailable");
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

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
            [],
            [],
            [],
            new ExecutionDecisionContext([], [], [], [], [], []));
        Harness harness = await CreateHarnessAsync(decisionProjection: projection);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        Assert.NotNull(context.DecisionProjection);
        Assert.Equal("DEC-0001", Assert.Single(context.DecisionProjection.Directives).DecisionId);
        Assert.Empty(context.Diagnostics.ValidationErrors);
    }

    [Fact]
    public async Task DecisionProjectionConflictsAreProjectedAsStructuredGovernanceBlockers()
    {
        var projection = new ExecutionDecisionProjection(
            Guid.Empty,
            DateTimeOffset.UtcNow,
            [],
            [],
            [],
            [],
            [
                new ExecutionDecisionConflict(
                    "conflict-1",
                    "DEC-0007",
                    "Persistence authority",
                    "Persistence mutations must remain backend-owned.",
                    "React recomputes persistence mutation eligibility.",
                    [
                        new DecisionSourceReference(
                            "Decision",
                            ".agents/decisions/decisions.md",
                            "Newly Authorized",
                            Excerpt: "Backend owns persistence mutation eligibility.")
                    ])
            ],
            [],
            new ExecutionDecisionContext([], [], [], [], [], []));
        Harness harness = await CreateHarnessAsync(decisionProjection: projection);
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m1.md", "milestone");

        ImplementationExecutionContext context = await harness.ContextService.BuildContextAsync(
            harness.Repository.Id);

        ExecutionGovernedConflictDiagnostic conflict = Assert.Single(context.Diagnostics.GovernedConflicts);
        Assert.Equal("conflict-1", conflict.Id);
        Assert.Equal("DEC-0007", conflict.DecisionId);
        Assert.Equal("React recomputes persistence mutation eligibility.", conflict.ConflictingExcerpt);
        Assert.Equal(".agents/decisions/decisions.md", conflict.AffectedContext);
        Assert.Equal("Governed Decision Projection", conflict.AffectedPromptSection);
        Assert.Equal("Blocking", conflict.Severity);
        Assert.Equal("DecisionProjectionService", conflict.OriginatingAuthority);
        Assert.Contains("Resolve or supersede", conflict.RecommendedResolution);
        Assert.Contains(conflict.Evidence, evidence => evidence.Contains("Persistence mutations", StringComparison.Ordinal));
        Assert.Contains(context.Diagnostics.ValidationErrors, error => error.Contains("DEC-0007", StringComparison.Ordinal));
        Assert.True(context.Diagnostics.LaunchBlocked);
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
        var contextService = new ImplementationExecutionContextService(
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
        string directory = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record Harness(Repository Repository, ImplementationExecutionContextService ContextService);

    private sealed class FakeGitService(RepositoryDirtyState? dirtyState, string? failure) : IGitService
    {
        public Task<RepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException(failure);
            }

            return Task.FromResult(new RepositorySnapshot
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

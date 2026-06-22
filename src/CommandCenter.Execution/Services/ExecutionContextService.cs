using System.Text;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Services;

public sealed class ExecutionContextService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IGitService gitService,
    IDecisionProjectionService? decisionProjectionService = null) : IExecutionContextService
{
    private const string PlanPath = ".agents/plan.md";
    private const string OperationalContextPath = ".agents/operational_context.md";
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";
    private const string CurrentDecisionsPath = ".agents/decisions/decisions.md";
    private const string MilestonesDirectory = ".agents/milestones";

    public async Task<ExecutionContext> BuildContextAsync(Guid repositoryId, string milestonePath)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var validationErrors = new List<string>();
        var missingOptionalArtifacts = new List<string>();
        var artifacts = new List<ExecutionContextArtifact>();
        ExecutionRepositorySnapshot? snapshot = null;
        ExecutionDecisionProjection? decisionProjection = null;
        string? milestoneContent = null;

        if (DetermineAvailability(repository) != RepositoryAvailability.Available)
        {
            validationErrors.Add("Repository is not available.");
        }

        ExecutionReadiness readiness = await planningService.DetermineReadinessAsync(repository);
        if (readiness != ExecutionReadiness.Ready)
        {
            validationErrors.Add($"Repository planning readiness is {readiness}.");
        }

        await AddRequiredArtifactAsync(repository, artifacts, "Plan", PlanPath, validationErrors);

        string normalizedMilestonePath = NormalizeRelativePath(milestonePath);
        if (!IsMilestonePath(repository, normalizedMilestonePath))
        {
            validationErrors.Add("Selected milestone path must stay within .agents/milestones.");
        }
        else
        {
            milestoneContent = await AddRequiredArtifactAsync(repository, artifacts, "Milestone", normalizedMilestonePath, validationErrors);
        }

        await AddOptionalArtifactAsync(repository, artifacts, "OperationalContext", OperationalContextPath, missingOptionalArtifacts);
        await AddOptionalArtifactAsync(repository, artifacts, "CurrentHandoff", CurrentHandoffPath, missingOptionalArtifacts);
        await AddOptionalArtifactAsync(repository, artifacts, "CurrentDecisions", CurrentDecisionsPath, missingOptionalArtifacts);

        try
        {
            snapshot = await gitService.GetSnapshotAsync(repository);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            validationErrors.Add($"Git snapshot failed: {exception.Message}");
        }

        if (decisionProjectionService is not null)
        {
            try
            {
                decisionProjection = await decisionProjectionService.BuildExecutionProjectionAsync(
                    repository.Id,
                    milestoneContent: milestoneContent);
                foreach (ExecutionDecisionConflict conflict in decisionProjection.Conflicts)
                {
                    validationErrors.Add(
                        $"Execution request conflicts with governed decision {conflict.DecisionId}: {conflict.ConflictingExcerpt}");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                validationErrors.Add($"Decision projection failed: {exception.Message}");
            }
        }

        ExecutionContextDiagnostics diagnostics = BuildDiagnostics(artifacts, validationErrors, missingOptionalArtifacts);

        return new ExecutionContext
        {
            RepositoryId = repository.Id,
            RepositoryName = repository.Name,
            RepositoryPath = repository.Path,
            MilestonePath = normalizedMilestonePath,
            GeneratedAt = generatedAt,
            Artifacts = artifacts,
            RepositorySnapshot = snapshot,
            DecisionProjection = decisionProjection,
            Diagnostics = diagnostics
        };
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<string?> AddRequiredArtifactAsync(
        Repository repository,
        List<ExecutionContextArtifact> artifacts,
        string role,
        string relativePath,
        List<string> validationErrors)
    {
        try
        {
            string content = await artifactService.LoadAsync(repository, relativePath);
            artifacts.Add(CreateArtifact(role, relativePath, content));
            return content;
        }
        catch (FileNotFoundException)
        {
            validationErrors.Add($"Required artifact is missing: {relativePath}");
            return null;
        }
        catch (ArgumentException exception)
        {
            validationErrors.Add(exception.Message);
            return null;
        }
    }

    private async Task AddOptionalArtifactAsync(
        Repository repository,
        List<ExecutionContextArtifact> artifacts,
        string role,
        string relativePath,
        List<string> missingOptionalArtifacts)
    {
        if (!await artifactService.ExistsAsync(repository, relativePath))
        {
            missingOptionalArtifacts.Add(relativePath);
            return;
        }

        artifacts.Add(CreateArtifact(role, relativePath, await artifactService.LoadAsync(repository, relativePath)));
    }

    private static ExecutionContextArtifact CreateArtifact(string role, string relativePath, string content)
    {
        return new ExecutionContextArtifact
        {
            Role = role,
            RelativePath = relativePath,
            Name = Path.GetFileName(relativePath),
            Content = content,
            ByteCount = Encoding.UTF8.GetByteCount(content),
            CharacterCount = content.Length
        };
    }

    private static ExecutionContextDiagnostics BuildDiagnostics(
        IReadOnlyList<ExecutionContextArtifact> artifacts,
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<string> missingOptionalArtifacts)
    {
        long totalBytes = artifacts.Sum(artifact => artifact.ByteCount);
        long totalCharacters = artifacts.Sum(artifact => artifact.CharacterCount);
        ExecutionContextArtifactDiagnostic[] artifactDiagnostics = artifacts.Select(artifact => new ExecutionContextArtifactDiagnostic
        {
            Role = artifact.Role,
            RelativePath = artifact.RelativePath,
            ByteCount = artifact.ByteCount,
            CharacterCount = artifact.CharacterCount,
            WarningThresholdBytes = ExecutionContextSizePolicy.ArtifactWarningThresholdBytes,
            HardLimitBytes = ExecutionContextSizePolicy.ArtifactHardLimitBytes,
            WarningThresholdExceeded = artifact.ByteCount > ExecutionContextSizePolicy.ArtifactWarningThresholdBytes,
            HardLimitExceeded = artifact.ByteCount > ExecutionContextSizePolicy.ArtifactHardLimitBytes
        }).ToArray();
        bool artifactHardLimitExceeded = artifactDiagnostics.Any(diagnostic => diagnostic.HardLimitExceeded);
        bool hardLimitExceeded = totalBytes > ExecutionContextSizePolicy.AggregateHardLimitBytes ||
                                 artifactHardLimitExceeded;

        return new ExecutionContextDiagnostics
        {
            TotalBytes = totalBytes,
            TotalCharacters = totalCharacters,
            WarningThresholdBytes = ExecutionContextSizePolicy.AggregateWarningThresholdBytes,
            HardLimitBytes = ExecutionContextSizePolicy.AggregateHardLimitBytes,
            WarningThresholdExceeded = totalBytes > ExecutionContextSizePolicy.AggregateWarningThresholdBytes ||
                artifactDiagnostics.Any(diagnostic => diagnostic.WarningThresholdExceeded),
            HardLimitExceeded = hardLimitExceeded,
            ArtifactDiagnostics = artifactDiagnostics,
            ValidationErrors = validationErrors.ToArray(),
            MissingOptionalArtifacts = missingOptionalArtifacts.ToArray(),
            LaunchBlocked = validationErrors.Count > 0 || hardLimitExceeded
        };
    }

    private static bool IsMilestonePath(Repository repository, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        try
        {
            string milestonePath = ArtifactPath.ResolveRepositoryPath(repository, relativePath);
            string milestonesRoot = ArtifactPath.ResolveRepositoryPath(repository, MilestonesDirectory);
            string relativeToMilestones = Path.GetRelativePath(milestonesRoot, milestonePath);

            return !relativeToMilestones.StartsWith("..", StringComparison.Ordinal) &&
                !Path.IsPathRooted(relativeToMilestones) &&
                relativePath.StartsWith($"{MilestonesDirectory}/", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static RepositoryAvailability DetermineAvailability(Repository repository)
    {
        if (!Directory.Exists(repository.Path))
        {
            return RepositoryAvailability.Missing;
        }

        try
        {
            _ = Directory.EnumerateFileSystemEntries(repository.Path).FirstOrDefault();
            string gitPath = Path.Combine(repository.Path, ".git");
            return Directory.Exists(gitPath) || File.Exists(gitPath)
                ? RepositoryAvailability.Available
                : RepositoryAvailability.Missing;
        }
        catch (UnauthorizedAccessException)
        {
            return RepositoryAvailability.AccessDenied;
        }
        catch (IOException)
        {
            return RepositoryAvailability.AccessDenied;
        }
    }
}

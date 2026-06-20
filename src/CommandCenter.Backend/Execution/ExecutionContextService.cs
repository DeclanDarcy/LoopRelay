using System.Text;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Execution;

public sealed class ExecutionContextService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IGitService gitService) : IExecutionContextService
{
    private const string PlanPath = ".agents/plan.md";
    private const string OperationalContextPath = ".agents/operational_context.md";
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";
    private const string CurrentDecisionsPath = ".agents/decisions/decisions.md";
    private const string MilestonesDirectory = ".agents/milestones";

    public async Task<ExecutionContext> BuildContextAsync(Guid repositoryId, string milestonePath)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var generatedAt = DateTimeOffset.UtcNow;
        var validationErrors = new List<string>();
        var missingOptionalArtifacts = new List<string>();
        var artifacts = new List<ExecutionContextArtifact>();
        ExecutionRepositorySnapshot? snapshot = null;

        if (DetermineAvailability(repository) != RepositoryAvailability.Available)
        {
            validationErrors.Add("Repository is not available.");
        }

        var readiness = await planningService.DetermineReadinessAsync(repository);
        if (readiness != ExecutionReadiness.Ready)
        {
            validationErrors.Add($"Repository planning readiness is {readiness}.");
        }

        await AddRequiredArtifactAsync(repository, artifacts, "Plan", PlanPath, validationErrors);

        var normalizedMilestonePath = NormalizeRelativePath(milestonePath);
        if (!IsMilestonePath(repository, normalizedMilestonePath))
        {
            validationErrors.Add("Selected milestone path must stay within .agents/milestones.");
        }
        else
        {
            await AddRequiredArtifactAsync(repository, artifacts, "Milestone", normalizedMilestonePath, validationErrors);
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

        var diagnostics = BuildDiagnostics(artifacts, validationErrors, missingOptionalArtifacts);

        return new ExecutionContext
        {
            RepositoryId = repository.Id,
            RepositoryName = repository.Name,
            RepositoryPath = repository.Path,
            MilestonePath = normalizedMilestonePath,
            GeneratedAt = generatedAt,
            Artifacts = artifacts,
            RepositorySnapshot = snapshot,
            Diagnostics = diagnostics
        };
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        var repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task AddRequiredArtifactAsync(
        Repository repository,
        List<ExecutionContextArtifact> artifacts,
        string role,
        string relativePath,
        List<string> validationErrors)
    {
        try
        {
            var content = await artifactService.LoadAsync(repository, relativePath);
            artifacts.Add(CreateArtifact(role, relativePath, content));
        }
        catch (FileNotFoundException)
        {
            validationErrors.Add($"Required artifact is missing: {relativePath}");
        }
        catch (ArgumentException exception)
        {
            validationErrors.Add(exception.Message);
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
        var totalBytes = artifacts.Sum(artifact => artifact.ByteCount);
        var totalCharacters = artifacts.Sum(artifact => artifact.CharacterCount);
        var artifactDiagnostics = artifacts.Select(artifact => new ExecutionContextArtifactDiagnostic
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
        var artifactHardLimitExceeded = artifactDiagnostics.Any(diagnostic => diagnostic.HardLimitExceeded);
        var hardLimitExceeded = totalBytes > ExecutionContextSizePolicy.AggregateHardLimitBytes ||
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
            var milestonePath = ArtifactPath.ResolveRepositoryPath(repository, relativePath);
            var milestonesRoot = ArtifactPath.ResolveRepositoryPath(repository, MilestonesDirectory);
            var relativeToMilestones = Path.GetRelativePath(milestonesRoot, milestonePath);

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
            var gitPath = Path.Combine(repository.Path, ".git");
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

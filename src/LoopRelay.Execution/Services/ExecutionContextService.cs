using System.Text;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Planning;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Execution.Abstractions;
using LoopRelay.Execution.Models;
using LoopRelay.Execution.Primitives;

namespace LoopRelay.Execution.Services;

public sealed class ImplementationExecutionContextService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IGitService gitService,
    IDecisionProjectionService? decisionProjectionService = null) : IImplementationExecutionContextService
{
    private const string PlanPath = ".agents/plan.md";
    // Optional plan companion, loaded right after the plan and injected into the {details} hole. Existence-guarded
    // (like the other optional artifacts) so a repository without a details.md is unaffected.
    private const string DetailsPath = ".agents/details.md";
    private const string OperationalContextPath = ".agents/operational_context.md";
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";

    // Canonical live decisions path (mirrors OrchestrationArtifactPaths.Decisions). Hardcoded rather than
    // referenced because the Execution layer must not depend on Orchestration (ArchitectureLayeringTests).
    private const string DecisionsPath = ".agents/decisions/decisions.md";

    public async Task<ImplementationExecutionContext> BuildContextAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var validationErrors = new List<string>();
        var governedConflicts = new List<ExecutionGovernedConflictDiagnostic>();
        var missingOptionalArtifacts = new List<string>();
        var artifacts = new List<LoadedArtifact>();
        RepositorySnapshot? snapshot = null;
        ExecutionDecisionProjection? decisionProjection = null;

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
        await AddOptionalArtifactAsync(repository, artifacts, "Details", DetailsPath, missingOptionalArtifacts);

        await AddOptionalArtifactAsync(repository, artifacts, "OperationalContext", OperationalContextPath, missingOptionalArtifacts);
        await AddOptionalArtifactAsync(repository, artifacts, "CurrentHandoff", CurrentHandoffPath, missingOptionalArtifacts);

        // Raw decisions.md feeds the prompt's {decisions} hole verbatim. The structured projection built
        // below stays only for launch-blocking conflict gating — it is no longer rendered into the prompt.
        await AddOptionalArtifactAsync(repository, artifacts, "Decisions", DecisionsPath, missingOptionalArtifacts);

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
                    repository.Id);
                foreach (ExecutionDecisionConflict conflict in decisionProjection.Conflicts)
                {
                    governedConflicts.Add(CreateGovernedConflictDiagnostic(conflict));
                    validationErrors.Add(
                        $"Execution request conflicts with governed decision {conflict.DecisionId}: {conflict.ConflictingExcerpt}");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                validationErrors.Add($"Decision projection failed: {exception.Message}");
            }
        }

        ImplementationExecutionContextDiagnostics diagnostics = BuildDiagnostics(
            artifacts,
            validationErrors,
            governedConflicts,
            missingOptionalArtifacts);

        return new ImplementationExecutionContext
        {
            Id = repository.Id,
            Name = repository.Name,
            Path = repository.Path,
            GeneratedAt = generatedAt,
            Artifacts = artifacts,
            Snapshot = snapshot,
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
        List<LoadedArtifact> artifacts,
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
        List<LoadedArtifact> artifacts,
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

    private static LoadedArtifact CreateArtifact(string role, string relativePath, string content)
    {
        return new LoadedArtifact
        {
            Role = role,
            RelativePath = relativePath,
            Name = Path.GetFileName(relativePath),
            Content = content,
            ByteCount = Encoding.UTF8.GetByteCount(content),
            CharacterCount = content.Length
        };
    }

    private static ImplementationExecutionContextDiagnostics BuildDiagnostics(
        IReadOnlyList<LoadedArtifact> artifacts,
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<ExecutionGovernedConflictDiagnostic> governedConflicts,
        IReadOnlyList<string> missingOptionalArtifacts)
    {
        long totalBytes = artifacts.Sum(artifact => artifact.ByteCount);
        long totalCharacters = artifacts.Sum(artifact => artifact.CharacterCount);
        ImplementationExecutionContextArtifactDiagnostic[] artifactDiagnostics = artifacts.Select(artifact => new ImplementationExecutionContextArtifactDiagnostic
        {
            Role = artifact.Role,
            RelativePath = artifact.RelativePath,
            ByteCount = artifact.ByteCount,
            CharacterCount = artifact.CharacterCount,
            WarningThresholdBytes = ImplementationExecutionContextSizePolicy.ArtifactWarningThresholdBytes,
            HardLimitBytes = ImplementationExecutionContextSizePolicy.ArtifactHardLimitBytes,
            WarningThresholdExceeded = artifact.ByteCount > ImplementationExecutionContextSizePolicy.ArtifactWarningThresholdBytes,
            HardLimitExceeded = artifact.ByteCount > ImplementationExecutionContextSizePolicy.ArtifactHardLimitBytes
        }).ToArray();
        bool artifactHardLimitExceeded = artifactDiagnostics.Any(diagnostic => diagnostic.HardLimitExceeded);
        bool hardLimitExceeded = totalBytes > ImplementationExecutionContextSizePolicy.AggregateHardLimitBytes ||
                                 artifactHardLimitExceeded;

        return new ImplementationExecutionContextDiagnostics
        {
            TotalBytes = totalBytes,
            TotalCharacters = totalCharacters,
            WarningThresholdBytes = ImplementationExecutionContextSizePolicy.AggregateWarningThresholdBytes,
            HardLimitBytes = ImplementationExecutionContextSizePolicy.AggregateHardLimitBytes,
            WarningThresholdExceeded = totalBytes > ImplementationExecutionContextSizePolicy.AggregateWarningThresholdBytes ||
                artifactDiagnostics.Any(diagnostic => diagnostic.WarningThresholdExceeded),
            HardLimitExceeded = hardLimitExceeded,
            ArtifactDiagnostics = artifactDiagnostics,
            ValidationErrors = validationErrors.ToArray(),
            GovernedConflicts = governedConflicts.ToArray(),
            MissingOptionalArtifacts = missingOptionalArtifacts.ToArray(),
            LaunchBlocked = validationErrors.Count > 0 || hardLimitExceeded
        };
    }

    private static ExecutionGovernedConflictDiagnostic CreateGovernedConflictDiagnostic(ExecutionDecisionConflict conflict)
    {
        string affectedContext = DetermineAffectedContext(conflict);
        string conflictReason = $"Governed decision {conflict.DecisionId} conflicts with selected execution context.";

        return new ExecutionGovernedConflictDiagnostic
        {
            Id = conflict.Id,
            DecisionId = conflict.DecisionId,
            Title = conflict.Title,
            Statement = conflict.Statement,
            ConflictingExcerpt = conflict.ConflictingExcerpt,
            ConflictReason = conflictReason,
            AffectedContext = affectedContext,
            AffectedPromptSection = "Governed Decision Projection",
            RecommendedResolution =
                "Resolve or supersede the governed decision conflict before launching execution.",
            Severity = "Blocking",
            OriginatingAuthority = "DecisionProjectionService",
            Sources = conflict.Sources,
            Evidence = BuildConflictEvidence(conflict),
            Diagnostics =
            [
                "Conflict was projected by the decisions authority and blocks execution context launch."
            ]
        };
    }

    private static string DetermineAffectedContext(ExecutionDecisionConflict conflict)
    {
        DecisionSourceReference? source = conflict.Sources.FirstOrDefault(source =>
            !string.IsNullOrWhiteSpace(source.RelativePath));

        return source?.RelativePath ?? "Selected milestone context";
    }

    private static IReadOnlyList<string> BuildConflictEvidence(ExecutionDecisionConflict conflict)
    {
        var evidence = new List<string>
        {
            $"Decision statement: {conflict.Statement}",
            $"Conflicting excerpt: {conflict.ConflictingExcerpt}"
        };

        foreach (DecisionSourceReference source in conflict.Sources)
        {
            if (!string.IsNullOrWhiteSpace(source.Excerpt))
            {
                evidence.Add($"Source excerpt: {source.Excerpt}");
            }
        }

        return evidence;
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

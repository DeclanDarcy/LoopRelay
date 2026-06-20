using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;
using System.Collections.Concurrent;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryProjectionService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IExecutionSessionService executionSessionService) : IRepositoryProjectionService
{
    private readonly ConcurrentDictionary<Guid, ArtifactInventory> inventoryCache = new();

    public async Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync()
    {
        var repositories = await repositoryService.GetAllAsync();
        var projections = new List<RepositoryDashboardProjection>();

        foreach (var repository in repositories)
        {
            var inventory = await GetOrBuildInventoryAsync(repository);
            projections.Add(new RepositoryDashboardProjection
            {
                Repository = repository,
                Availability = DetermineAvailability(repository),
                Readiness = await planningService.DetermineReadinessAsync(repository),
                ExecutionState = await executionSessionService.GetRepositoryStateAsync(repository.Id),
                ActiveExecutionSession = await executionSessionService.GetActiveSessionAsync(repository.Id),
                ExecutionSummary = await executionSessionService.GetRepositorySessionSummaryAsync(repository.Id),
                MilestoneCount = inventory.Milestones.Count,
                HasCurrentHandoff = inventory.CurrentHandoff is not null,
                HasCurrentDecisions = inventory.CurrentDecisions is not null
            });
        }

        return projections;
    }

    public async Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        return await BuildWorkspaceProjectionAsync(repository, await GetOrBuildInventoryAsync(repository));
    }

    public async Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId)
    {
        var repository = await GetRepositoryAsync(repositoryId);
        var inventory = await BuildInventoryAsync(repository);
        inventoryCache[repository.Id] = inventory;
        return await BuildWorkspaceProjectionAsync(repository, inventory);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        var repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<RepositoryWorkspaceProjection> BuildWorkspaceProjectionAsync(
        Repository repository,
        ArtifactInventory inventory)
    {
        return new RepositoryWorkspaceProjection
        {
            Repository = repository,
            Availability = DetermineAvailability(repository),
            Readiness = await planningService.DetermineReadinessAsync(repository),
            ExecutionState = await executionSessionService.GetRepositoryStateAsync(repository.Id),
            ExecutionSummary = await executionSessionService.GetRepositorySessionSummaryAsync(repository.Id),
            ArtifactInventory = inventory,
            MilestoneCount = inventory.Milestones.Count,
            HasPlan = inventory.Plan is not null,
            HasOperationalContext = inventory.OperationalContext is not null,
            HasCurrentHandoff = inventory.CurrentHandoff is not null,
            HasCurrentDecisions = inventory.CurrentDecisions is not null
        };
    }

    private async Task<ArtifactInventory> GetOrBuildInventoryAsync(Repository repository)
    {
        if (inventoryCache.TryGetValue(repository.Id, out var inventory))
        {
            return inventory;
        }

        inventory = await BuildInventoryAsync(repository);
        inventoryCache[repository.Id] = inventory;
        return inventory;
    }

    private async Task<ArtifactInventory> BuildInventoryAsync(Repository repository)
    {
        var artifacts = await artifactService.DiscoverAsync(repository);

        return new ArtifactInventory
        {
            Plan = artifacts.SingleOrDefault(artifact => artifact.Type == ArtifactType.Plan),
            OperationalContext = artifacts.SingleOrDefault(artifact => artifact.Type == ArtifactType.OperationalContext),
            Milestones = artifacts
                .Where(artifact => artifact.Type == ArtifactType.Milestone)
                .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CurrentHandoff = artifacts.SingleOrDefault(artifact =>
                artifact.Family == ArtifactFamily.Handoff &&
                artifact.VersionKind == ArtifactVersionKind.Current),
            HistoricalHandoffs = artifacts
                .Where(artifact =>
                    artifact.Family == ArtifactFamily.Handoff &&
                    artifact.VersionKind == ArtifactVersionKind.Historical)
                .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CurrentDecisions = artifacts.SingleOrDefault(artifact =>
                artifact.Family == ArtifactFamily.Decision &&
                artifact.VersionKind == ArtifactVersionKind.Current),
            HistoricalDecisions = artifacts
                .Where(artifact =>
                    artifact.Family == ArtifactFamily.Decision &&
                    artifact.VersionKind == ArtifactVersionKind.Historical)
                .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
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

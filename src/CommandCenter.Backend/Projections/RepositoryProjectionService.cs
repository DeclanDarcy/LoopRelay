using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Continuity;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;
using System.Collections.Concurrent;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryProjectionService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IExecutionSessionService executionSessionService,
    IOperationalContextProposalStore operationalContextProposalStore,
    IOperationalContextParser operationalContextParser,
    IArtifactStore artifactStore) : IRepositoryProjectionService
{
    private readonly ConcurrentDictionary<Guid, ArtifactInventory> inventoryCache = new();

    public async Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync()
    {
        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        var projections = new List<RepositoryDashboardProjection>();

        foreach (Repository repository in repositories)
        {
            ArtifactInventory inventory = await GetOrBuildInventoryAsync(repository);
            OperationalContextProjection operationalContext = await BuildOperationalContextProjectionAsync(repository, inventory);
            projections.Add(new RepositoryDashboardProjection
            {
                Repository = repository,
                Availability = DetermineAvailability(repository),
                Readiness = await planningService.DetermineReadinessAsync(repository),
                ExecutionState = await executionSessionService.GetRepositoryStateAsync(repository.Id),
                ActiveExecutionSession = await executionSessionService.GetActiveSessionAsync(repository.Id),
                ExecutionSummary = await executionSessionService.GetRepositorySessionSummaryAsync(repository.Id),
                ExecutionHistory = await executionSessionService.GetRepositorySessionHistoryAsync(repository.Id),
                MilestoneCount = inventory.Milestones.Count,
                HasCurrentHandoff = inventory.CurrentHandoff is not null,
                HasCurrentDecisions = inventory.CurrentDecisions is not null,
                ContinuitySummary = new RepositoryContinuitySummary
                {
                    OperationalContextExists = operationalContext.Exists,
                    OperationalContextRevisionCount = operationalContext.RevisionCount,
                    OperationalContextLastUpdatedAt = operationalContext.LastUpdatedAt,
                    OpenQuestionCount = operationalContext.OpenQuestions.Count,
                    ActiveRiskCount = operationalContext.ActiveRisks.Count,
                    PendingProposalExists = operationalContext.PendingProposalSummary.PendingProposalExists
                }
            });
        }

        return projections;
    }

    public async Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildWorkspaceProjectionAsync(repository, await GetOrBuildInventoryAsync(repository));
    }

    public async Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ArtifactInventory inventory = await BuildInventoryAsync(repository);
        inventoryCache[repository.Id] = inventory;
        return await BuildWorkspaceProjectionAsync(repository, inventory);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<RepositoryWorkspaceProjection> BuildWorkspaceProjectionAsync(
        Repository repository,
        ArtifactInventory inventory)
    {
        OperationalContextProjection operationalContext = await BuildOperationalContextProjectionAsync(repository, inventory);
        return new RepositoryWorkspaceProjection
        {
            Repository = repository,
            Availability = DetermineAvailability(repository),
            Readiness = await planningService.DetermineReadinessAsync(repository),
            ExecutionState = await executionSessionService.GetRepositoryStateAsync(repository.Id),
            ExecutionSummary = await executionSessionService.GetRepositorySessionSummaryAsync(repository.Id),
            ExecutionHistory = await executionSessionService.GetRepositorySessionHistoryAsync(repository.Id),
            ArtifactInventory = inventory,
            MilestoneCount = inventory.Milestones.Count,
            HasPlan = inventory.Plan is not null,
            HasOperationalContext = inventory.OperationalContext is not null,
            HasCurrentHandoff = inventory.CurrentHandoff is not null,
            HasCurrentDecisions = inventory.CurrentDecisions is not null,
            OperationalContextProposalSummary = operationalContext.PendingProposalSummary,
            OperationalContext = operationalContext
        };
    }

    private async Task<OperationalContextProjection> BuildOperationalContextProjectionAsync(
        Repository repository,
        ArtifactInventory inventory)
    {
        OperationalContextProposal? latestProposal = (await operationalContextProposalStore.ListAsync(repository)).FirstOrDefault();
        OperationalContextProposalSummary proposalSummary = BuildOperationalContextProposalSummary(latestProposal);
        Artifact? current = inventory.OperationalContext;
        int revisionCount = inventory.HistoricalOperationalContexts.Count + (current is null ? 0 : 1);
        int currentRevisionNumber = current is null
            ? 0
            : GetHighestHistoricalRevisionNumber(inventory.HistoricalOperationalContexts) + 1;

        if (current is null)
        {
            return new OperationalContextProjection
            {
                Exists = false,
                RevisionCount = revisionCount,
                CurrentRevisionNumber = currentRevisionNumber,
                LastPromotionAt = proposalSummary.LastPromotedAt,
                PendingProposalSummary = proposalSummary,
                LatestReviewState = latestProposal?.Review.ReviewState,
                ContinuityWarnings = latestProposal?.CompressionSummary.Warnings ?? []
            };
        }

        string currentPath = ArtifactPath.ResolveRepositoryPath(repository, current.RelativePath);
        string content = await artifactStore.ReadAsync(currentPath) ?? string.Empty;
        OperationalContextDocument document = operationalContextParser.Parse(content);

        return new OperationalContextProjection
        {
            Exists = true,
            CurrentRelativePath = current.RelativePath,
            RevisionCount = revisionCount,
            CurrentRevisionNumber = currentRevisionNumber,
            LastUpdatedAt = GetLastWriteTime(currentPath),
            LastPromotionAt = proposalSummary.LastPromotedAt,
            CurrentUnderstandingSummary = document.CurrentMentalModel.Select(item => item.Text).ToArray(),
            Architecture = document.Architecture,
            AuthorityBoundaries = document.AuthorityBoundaries,
            Constraints = document.Constraints,
            StableDecisions = document.StableDecisions,
            DecisionRationale = document.DecisionRationale,
            OpenQuestions = document.OpenQuestions,
            ActiveRisks = document.ActiveRisks,
            RecentUnderstandingChanges = document.RecentUnderstandingChanges,
            PendingProposalSummary = proposalSummary,
            LatestReviewState = latestProposal?.Review.ReviewState,
            ContinuityWarnings = latestProposal?.CompressionSummary.Warnings ?? []
        };
    }

    private static OperationalContextProposalSummary BuildOperationalContextProposalSummary(
        OperationalContextProposal? latestProposal)
    {
        if (latestProposal is null)
        {
            return new OperationalContextProposalSummary();
        }

        OperationalContextInputFingerprint? generatedFingerprint = latestProposal.InputFingerprints
            .FirstOrDefault(fingerprint => fingerprint.Name == "GeneratedProposal");
        return new OperationalContextProposalSummary
        {
            PendingProposalExists = latestProposal.Status == OperationalContextProposalStatus.Pending,
            LatestProposalId = latestProposal.ProposalId,
            GeneratedAt = latestProposal.GeneratedAt,
            Status = latestProposal.Status,
            SourceInputCount = latestProposal.InputFingerprints.Count(fingerprint =>
                fingerprint.Present &&
                fingerprint.Name != "GeneratedProposal"),
            ContentByteCount = generatedFingerprint?.ByteCount ?? 0,
            ContentCharacterCount = generatedFingerprint?.CharacterCount ?? 0,
            LastPromotedAt = latestProposal.Promotion.PromotedAt,
            LastArchivedRelativePath = latestProposal.Promotion.ArchivedRelativePath
        };
    }

    private static DateTimeOffset? GetLastWriteTime(string path)
    {
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
    }

    private static int GetHighestHistoricalRevisionNumber(IReadOnlyList<Artifact> historicalOperationalContexts)
    {
        return historicalOperationalContexts
            .Select(artifact => Path.GetFileNameWithoutExtension(artifact.RelativePath))
            .Select(name => name.LastIndexOf('.') is var index && index >= 0
                ? name[(index + 1)..]
                : string.Empty)
            .Select(text => int.TryParse(text, out int number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private async Task<ArtifactInventory> GetOrBuildInventoryAsync(Repository repository)
    {
        if (inventoryCache.TryGetValue(repository.Id, out ArtifactInventory? inventory))
        {
            return inventory;
        }

        inventory = await BuildInventoryAsync(repository);
        inventoryCache[repository.Id] = inventory;
        return inventory;
    }

    private async Task<ArtifactInventory> BuildInventoryAsync(Repository repository)
    {
        IReadOnlyList<Artifact> artifacts = await artifactService.DiscoverAsync(repository);

        return new ArtifactInventory
        {
            Plan = artifacts.SingleOrDefault(artifact => artifact.Type == ArtifactType.Plan),
            OperationalContext = artifacts.SingleOrDefault(artifact =>
                artifact.Family == ArtifactFamily.OperationalContext &&
                artifact.VersionKind == ArtifactVersionKind.Current),
            HistoricalOperationalContexts = artifacts
                .Where(artifact =>
                    artifact.Family == ArtifactFamily.OperationalContext &&
                    artifact.VersionKind == ArtifactVersionKind.Historical)
                .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
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

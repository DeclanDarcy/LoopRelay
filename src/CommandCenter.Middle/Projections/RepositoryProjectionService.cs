using CommandCenter.Core.Artifacts;
using CommandCenter.Continuity;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using System.Collections.Concurrent;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Middle.Projections;

public sealed class RepositoryProjectionService(
    IRepositoryService repositoryService,
    IArtifactService artifactService,
    IPlanningService planningService,
    IExecutionSessionService executionSessionService,
    IOperationalContextProposalStore operationalContextProposalStore,
    IOperationalContextParser operationalContextParser,
    IArtifactStore artifactStore,
    IDecisionArtifactProjectionService? decisionArtifactProjectionService = null,
    IReasoningRepository? reasoningRepository = null,
    IDecisionSessionObservabilityService? decisionSessionObservabilityService = null) : IRepositoryProjectionService
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
            RepositoryReasoningSummary reasoningSummary = await BuildReasoningSummaryAsync(repository);
            RepositoryDecisionSessionSummary decisionSessionSummary = await BuildDecisionSessionSummaryAsync(repository);
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
                },
                ReasoningSummary = reasoningSummary,
                DecisionSessionSummary = decisionSessionSummary
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
        RepositoryReasoningSummary reasoningSummary = await BuildReasoningSummaryAsync(repository);
        RepositoryDecisionSessionSummary decisionSessionSummary = await BuildDecisionSessionSummaryAsync(repository);
        MilestoneProgressRollup milestoneProgress = await BuildMilestoneProgressAsync(repository, inventory);
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
            MilestoneProgress = milestoneProgress,
            HasPlan = inventory.Plan is not null,
            HasOperationalContext = inventory.OperationalContext is not null,
            HasCurrentHandoff = inventory.CurrentHandoff is not null,
            HasCurrentDecisions = inventory.CurrentDecisions is not null,
            OperationalContextProposalSummary = operationalContext.PendingProposalSummary,
            OperationalContext = operationalContext,
            ReasoningSummary = reasoningSummary,
            DecisionSessionSummary = decisionSessionSummary
        };
    }

    // Read-only milestone progress: read each milestone file and count its GitHub-style task
    // checkboxes. Agents own milestone agency (they check the boxes); this only reports the state.
    // Reads content via the already-injected artifactStore — the same pattern operational context uses.
    private async Task<MilestoneProgressRollup> BuildMilestoneProgressAsync(
        Repository repository,
        ArtifactInventory inventory)
    {
        var milestones = new List<MilestoneProgress>(inventory.Milestones.Count);
        foreach (Artifact milestone in inventory.Milestones)
        {
            string path = ArtifactPath.ResolveRepositoryPath(repository, milestone.RelativePath);
            string content = await artifactStore.ReadAsync(path) ?? string.Empty;
            (int completed, int total) = CountCheckboxes(content);
            milestones.Add(new MilestoneProgress
            {
                RelativePath = milestone.RelativePath,
                Name = milestone.Name,
                CompletedTaskCount = completed,
                TotalTaskCount = total,
                IsComplete = total > 0 && completed == total
            });
        }

        return new MilestoneProgressRollup
        {
            CompletedMilestoneCount = milestones.Count(milestone => milestone.IsComplete),
            TotalMilestoneCount = milestones.Count,
            Milestones = milestones
        };
    }

    // Counts GitHub-flavored task-list items ("- [ ] " / "- [x] "), ignoring anything inside fenced
    // code blocks. A task is an outdented/indented hyphen bullet, one space, "[", one mark char, "]",
    // one space; checked when the mark is 'x' or 'X'. Nested items count individually.
    private static (int Completed, int Total) CountCheckboxes(string content)
    {
        int completed = 0;
        int total = 0;
        bool insideFence = false;

        foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> line = rawLine.TrimStart();
            if (line.StartsWith("```"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || line.Length < 6)
            {
                continue;
            }

            if (line[0] != '-' || line[1] != ' ' || line[2] != '[' || line[4] != ']' || line[5] != ' ')
            {
                continue;
            }

            char mark = line[3];
            if (mark == ' ')
            {
                total++;
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (completed, total);
    }

    private async Task<RepositoryDecisionSessionSummary> BuildDecisionSessionSummaryAsync(Repository repository)
    {
        if (decisionSessionObservabilityService is null)
        {
            return new RepositoryDecisionSessionSummary();
        }

        DecisionSessionLifecycleProjection projection = await decisionSessionObservabilityService.GetProjectionAsync(repository.Id);
        DecisionSessionHealthAssessment health = await decisionSessionObservabilityService.GetHealthAsync(repository.Id);

        return new RepositoryDecisionSessionSummary
        {
            DecisionSessionId = projection.ActiveSession?.Id.ToString(),
            State = projection.ActiveSession?.State.ToString(),
            LifecycleDecision = projection.Policy?.Evaluation.Decision.ToString(),
            TransferEligibilityStatus = projection.TransferEligibility?.Eligibility.Status.ToString(),
            EstimatedTokenCount = projection.Size?.EstimatedTokenCount ?? projection.Metrics?.Metrics.EstimatedTokenCount,
            EstimatedCacheTtl = projection.Metrics?.Cache.EstimatedCacheTtl,
            CacheMissRisk = projection.Size?.CacheMissRisk ?? projection.Metrics?.Cache.EstimatedCacheMissRisk,
            CoherenceScore = projection.Coherence?.Coherence.CoherenceScore,
            TransferPressure = projection.Coherence?.Coherence.TransferPressure,
            HealthDimensions = health.Dimensions
                .Select(dimension => new RepositoryDecisionSessionHealthDimension
                {
                    Name = dimension.Name,
                    Status = dimension.Status.ToString(),
                    Findings = dimension.Findings
                })
                .ToArray(),
            RecentTransferLineage = projection.TransferEvents
                .Select(transfer => new RepositoryDecisionSessionTransferSummary
                {
                    TransferId = transfer.TransferId,
                    SourceSessionId = transfer.SourceSessionId.ToString(),
                    TargetSessionId = transfer.TargetSessionId?.ToString(),
                    ContinuityArtifactId = transfer.ContinuityArtifactId,
                    StartedAt = transfer.StartedAt,
                    CompletedAt = transfer.CompletedAt,
                    Succeeded = transfer.Succeeded
                })
                .ToArray(),
            Diagnostics = projection.Diagnostics.Errors
                .Concat(projection.Diagnostics.Warnings)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            GeneratedAt = projection.GeneratedAt
        };
    }

    private async Task<RepositoryReasoningSummary> BuildReasoningSummaryAsync(Repository repository)
    {
        if (reasoningRepository is null)
        {
            return new RepositoryReasoningSummary();
        }

        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> threads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);

        DateTimeOffset? lastEventAt = MaxOrNull(events.Select(reasoningEvent => (DateTimeOffset?)reasoningEvent.CreatedAt));
        DateTimeOffset? lastThreadActivityAt = MaxOrNull(threads.Select(thread => (DateTimeOffset?)thread.UpdatedAt));
        DateTimeOffset? lastRelationshipAt = MaxOrNull(relationships.Select(relationship => (DateTimeOffset?)relationship.CreatedAt));

        return new RepositoryReasoningSummary
        {
            EventCount = events.Count,
            ThreadCount = threads.Count,
            RelationshipCount = relationships.Count,
            HypothesisEventCount = CountFamily(events, ReasoningEventFamily.Hypothesis),
            AlternativeEventCount = CountFamily(events, ReasoningEventFamily.Alternative),
            ContradictionEventCount = CountFamily(events, ReasoningEventFamily.Contradiction),
            DirectionEventCount = CountFamily(events, ReasoningEventFamily.Direction),
            DecisionEvolutionEventCount = CountFamily(events, ReasoningEventFamily.DecisionEvolution),
            AssumptionEvolutionEventCount = CountFamily(events, ReasoningEventFamily.AssumptionEvolution),
            ConstraintEvolutionEventCount = CountFamily(events, ReasoningEventFamily.ConstraintEvolution),
            EvidenceEventCount = CountFamily(events, ReasoningEventFamily.Evidence),
            LastEventAt = lastEventAt,
            LastThreadActivityAt = lastThreadActivityAt,
            LastRelationshipAt = lastRelationshipAt,
            LastActivityAt = MaxOrNull([lastEventAt, lastThreadActivityAt, lastRelationshipAt])
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

    private static int CountFamily(IReadOnlyList<ReasoningEvent> events, ReasoningEventFamily family)
    {
        return events.Count(reasoningEvent => reasoningEvent.Family == family);
    }

    private static DateTimeOffset? MaxOrNull(IEnumerable<DateTimeOffset?> values)
    {
        return values.Max();
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
        if (decisionArtifactProjectionService is not null)
        {
            await decisionArtifactProjectionService.RecoverMissingProjectionsAsync(repository);
        }

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
                .ToArray(),
            ReasoningArtifacts = artifacts
                .Where(artifact => artifact.Family == ArtifactFamily.Reasoning)
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

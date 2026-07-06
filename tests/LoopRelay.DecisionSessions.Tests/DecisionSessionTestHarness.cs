using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Continuity.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Services;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.DecisionSessions.Services;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;

namespace LoopRelay.DecisionSessions.Tests;

internal sealed record DecisionSessionTestHarness(
    Repository Repository,
    MemoryArtifactStore Store,
    DecisionSessionTestRepositoryService RepositoryService,
    FileSystemDecisionSessionRepository RepositoryStore,
    DecisionSessionRegistry Registry,
    DecisionSessionRecoveryService Recovery)
{
    public static DecisionSessionTestHarness Create()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"))
        };
        Directory.CreateDirectory(repository.Path);
        var store = new MemoryArtifactStore();
        var repositoryService = new DecisionSessionTestRepositoryService(repository);
        var sessionRepository = new FileSystemDecisionSessionRepository(store);
        var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
        var recovery = new DecisionSessionRecoveryService(repositoryService, sessionRepository, TimeProvider.System);
        return new DecisionSessionTestHarness(repository, store, repositoryService, sessionRepository, registry, recovery);
    }

    public static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    // Builds the full decision-session analysis stack over real evidence readers (no derived-snapshot cache:
    // the services fall back to pure compute-on-read). Phase 3 (refactor-lazy-sqlite.md): DecisionSession
    // ObservabilityService now ACTIVELY computes its snapshots through these providers instead of reading
    // pre-warmed files, so the cold-cache observability tests drive this real stack.
    public DecisionSessionObservabilityService CreateObservabilityService(TimeProvider timeProvider)
    {
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(Store);
        var artifactService = new ArtifactService(Store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        var metricsService = new DecisionSessionMetricsService(
            RepositoryService, Registry, RepositoryStore, evidenceReader, new DeterministicTokenEstimator(), timeProvider);
        var economicsService = new DecisionSessionEconomicsService(
            RepositoryService, RepositoryStore, metricsService, new DecisionSessionEconomicsOptions(), timeProvider);
        var graphService = new ReasoningGraphService(RepositoryService, reasoningRepository, Store);
        var coherenceService = new DecisionSessionCoherenceService(
            RepositoryService, RepositoryStore, metricsService, economicsService, graphService, new DecisionSessionCoherenceOptions(), timeProvider);
        var lifecyclePolicy = new DecisionSessionLifecyclePolicy(
            RepositoryService, RepositoryStore, metricsService, economicsService, coherenceService, new DecisionSessionLifecyclePolicyOptions(), timeProvider);
        var recoveryService = new DecisionSessionRecoveryService(RepositoryService, RepositoryStore, timeProvider);
        var eligibilityService = new DecisionSessionTransferEligibilityService(
            RepositoryService, RepositoryStore, recoveryService, lifecyclePolicy, evidenceReader, timeProvider);
        return new DecisionSessionObservabilityService(
            RepositoryService,
            RepositoryStore,
            timeProvider,
            metricsService,
            economicsService,
            coherenceService,
            lifecyclePolicy,
            eligibilityService);
    }

    public async Task WriteRegistryAsync(
        IReadOnlyList<DecisionSession> sessions,
        Guid? documentRepositoryId = null,
        string schemaVersion = DecisionSessionArtifactPaths.SchemaVersion)
    {
        var document = new DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>(
            schemaVersion,
            documentRepositoryId ?? Repository.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            sessions.Select(session => new DecisionSessionRecord(session)).ToArray());
        await Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(Repository, DecisionSessionArtifactPaths.RegistryJson()),
            JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }
}

internal sealed class DecisionSessionTestRepositoryService(params Repository[] repositories) : IRepositoryService
{
    public Task<IReadOnlyList<Repository>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<Repository>>(repositories);
    }

    public Task<Repository> RegisterAsync(string repositoryPath)
    {
        throw new NotSupportedException();
    }

    public Task RemoveAsync(Guid repositoryId)
    {
        throw new NotSupportedException();
    }
}

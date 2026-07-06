using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverCreatesCandidateFromDecisionContextSignal()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect missing direction.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);

        DecisionDiscoveryResult result = await service.DiscoverAsync(repository.Id);

        DecisionCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal("CAND-0001", candidate.Id);
        Assert.Equal(DecisionCandidateState.Discovered, candidate.State);
        Assert.Equal(DecisionCandidatePriority.High, candidate.Priority);
        Assert.Equal(DecisionClassification.Architectural, candidate.Classification);
        Assert.Contains(candidate.Signals, signal => signal.Kind == "MissingDirection");
        Assert.NotEmpty(candidate.Evidence);
        Assert.Contains(candidate.Sources, source =>
            source.RelativePath == ".agents/plan.md" &&
            source.Excerpt == "Need to decide repository-backed persistence schema.");
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "candidates", "CAND-0001", "candidate.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "candidates", "CAND-0001", "candidate.md")));
        Assert.Equal(1, result.Diagnostics.CreatedCandidateCount);
    }

    [Fact]
    public async Task DiscoverSuppressesDuplicateActiveCandidates()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Blocked until API direction is decided.");
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect blocked execution.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);

        DecisionDiscoveryResult first = await service.DiscoverAsync(repository.Id);
        DecisionDiscoveryResult second = await service.DiscoverAsync(repository.Id);
        IReadOnlyList<DecisionCandidate> persisted = await decisionRepository.ListCandidatesAsync(repository);

        Assert.Single(first.Candidates);
        Assert.Empty(second.Candidates);
        Assert.Equal(1, second.Diagnostics.SuppressedDuplicateCount);
        Assert.Single(persisted);
    }

    [Fact]
    public async Task CandidateLifecycleTransitionsSurviveRestart()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Ambiguous execution workflow needs direction.");
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect ambiguity.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);
        DecisionCandidate candidate = Assert.Single((await service.DiscoverAsync(repository.Id)).Candidates);

        DecisionCandidate dismissed = await service.DismissCandidateAsync(repository.Id, candidate.Id, "Not worth pursuing now.");
        var restartedService = CreateService(repository, store, new FileSystemDecisionRepository(store));
        IReadOnlyList<DecisionCandidate> candidates = await restartedService.ListCandidatesAsync(repository.Id);

        DecisionCandidate reloaded = Assert.Single(candidates);
        Assert.Equal(DecisionCandidateState.Dismissed, dismissed.State);
        Assert.Equal(DecisionCandidateState.Dismissed, reloaded.State);
        Assert.Contains(reloaded.History, entry => entry.Event == "Dismissed" && entry.Reason == "Not worth pursuing now.");
    }

    [Fact]
    public async Task ExpiredCandidateDoesNotRediscoverAsActiveWork()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide operational workflow ownership.");
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect missing direction.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);
        DecisionCandidate candidate = Assert.Single((await service.DiscoverAsync(repository.Id)).Candidates);

        DecisionCandidate expired = await service.ExpireCandidateAsync(repository.Id, candidate.Id, "Source blocker resolved.");
        DecisionDiscoveryResult rediscovery = await service.DiscoverAsync(repository.Id);
        IReadOnlyList<DecisionCandidate> persisted = await service.ListCandidatesAsync(repository.Id);

        Assert.Equal(DecisionCandidateState.Expired, expired.State);
        Assert.Empty(rediscovery.Candidates);
        Assert.Equal(1, rediscovery.Diagnostics.SuppressedDuplicateCount);
        DecisionCandidate reloaded = Assert.Single(persisted);
        Assert.Equal(DecisionCandidateState.Expired, reloaded.State);
        Assert.Contains(reloaded.History, entry => entry.Event == "Expired" && entry.Reason == "Source blocker resolved.");
    }

    [Fact]
    public async Task DismissedExpiredAndDuplicateCandidatesDoNotAccumulateAsActiveWork()
    {
        Repository repository = CreateRepository();
        await WriteAsync(
            repository,
            ".agents/plan.md",
            """
            # Plan

            - Need to decide repository-backed API schema.
            - Ambiguous execution workflow.
            - Blocked until governance policy is decided.
            - Conflict between backend API approaches.
            """);
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect candidate lifecycle hygiene.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);
        DecisionDiscoveryResult firstDiscovery = await service.DiscoverAsync(repository.Id);
        DecisionCandidate[] candidates = firstDiscovery.Candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();

        await service.DismissCandidateAsync(repository.Id, candidates[0].Id, "Dismissed by reviewer.");
        await service.ExpireCandidateAsync(repository.Id, candidates[1].Id, "Expired after source changed.");
        await service.MarkCandidateDuplicateAsync(repository.Id, candidates[2].Id, candidates[3].Id, "Covered by another candidate.");
        DecisionDiscoveryResult rediscovery = await service.DiscoverAsync(repository.Id);
        IReadOnlyList<DecisionCandidate> persisted = await service.ListCandidatesAsync(repository.Id);

        Assert.Equal(4, candidates.Length);
        Assert.Empty(rediscovery.Candidates);
        Assert.Equal(4, rediscovery.Diagnostics.SuppressedDuplicateCount);
        Assert.Collection(
            persisted.OrderBy(candidate => candidate.Id, StringComparer.Ordinal),
            candidate => Assert.Equal(DecisionCandidateState.Dismissed, candidate.State),
            candidate => Assert.Equal(DecisionCandidateState.Expired, candidate.State),
            candidate => Assert.Equal(DecisionCandidateState.Duplicate, candidate.State),
            candidate => Assert.Equal(DecisionCandidateState.Discovered, candidate.State));
    }

    private static DecisionDiscoveryService CreateService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var contextService = new DecisionContextService(repositoryService, store, decisionRepository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionDiscoveryService(repositoryService, contextService, decisionRepository, projectionService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
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
}

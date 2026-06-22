using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

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
    public async Task InvalidCandidateTransitionReturnsConflictFromEndpoint()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Ambiguous API direction.");
        await WriteAsync(repository, ".agents/milestones/m2-decision-discovery.md", "# M2\n\n- Detect ambiguity.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        HttpResponseMessage discoverResponse = await client.PostAsync($"{root}/api/repositories/{repository.Id}/decisions/discover", null);
        DecisionDiscoveryResult? result = await discoverResponse.Content.ReadFromJsonAsync<DecisionDiscoveryResult>(jsonOptions);
        Assert.NotNull(result);
        DecisionCandidate candidate = Assert.Single(result.Candidates);

        HttpResponseMessage dismissResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/dismiss",
            new DecisionCandidateTransitionRequest("Dismiss once."));
        HttpResponseMessage promoteResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/promote",
            new DecisionCandidateTransitionRequest("Invalid after dismissal."));

        Assert.Equal(HttpStatusCode.OK, dismissResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, promoteResponse.StatusCode);
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
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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

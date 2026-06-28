using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionLifecycleEndpointTests
{
    [Fact]
    public async Task CoreLifecycleEndpointsExecuteEndToEndDecisionPath()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", """
            # Plan

            - Need to decide repository-backed persistence schema.
            """);
        await using WebApplication app = await CreateApp(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionDiscoveryResult discovery = await ReadOkAsync<DecisionDiscoveryResult>(
            await client.PostAsync($"{root}/api/repositories/{repository.Id}/decisions/discover", null),
            jsonOptions);
        DecisionCandidate discovered = Assert.Single(discovery.Candidates);
        DecisionCandidate promoted = await ReadOkAsync<DecisionCandidate>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/{discovered.Id}/promote",
                new DecisionCandidateTransitionRequest("Promote through endpoint."),
                jsonOptions),
            jsonOptions);
        DecisionProposal proposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/{promoted.Id}/proposals",
                null),
            jsonOptions);
        DecisionReviewWorkspace viewed = await ReadOkAsync<DecisionReviewWorkspace>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/viewed",
                new DecisionProposalTransitionRequest("Viewed through endpoint."),
                jsonOptions),
            jsonOptions);
        DecisionReviewWorkspace needsRefinement = await ReadOkAsync<DecisionReviewWorkspace>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/needs-refinement",
                new DecisionProposalTransitionRequest("Needs endpoint refinement."),
                jsonOptions),
            jsonOptions);
        DecisionProposal refined = await ReadOkAsync<DecisionProposal>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/refinements",
                new DecisionRefinementRequest(
                    "Refine through endpoint.",
                    Context: "Refined context from lifecycle endpoint characterization.",
                    RequestedBy: "endpoint-test"),
                jsonOptions),
            jsonOptions);
        DecisionReviewWorkspace ready = await ReadOkAsync<DecisionReviewWorkspace>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/ready-for-resolution",
                new DecisionProposalTransitionRequest("Ready through endpoint."),
                jsonOptions),
            jsonOptions);
        Decision firstDecision = await ReadOkAsync<Decision>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/resolve",
                new ResolveDecisionCommand("Resolve through endpoint.", "endpoint-test", "option-1"),
                jsonOptions),
            jsonOptions);

        Decision replacementDecision = await ResolveReplacementDecisionAsync(root, client, jsonOptions, repository);
        Decision superseded = await ReadOkAsync<Decision>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/{firstDecision.Id.Value}/supersede",
                new SupersedeDecisionCommand(replacementDecision.Id.Value, "Replacement supersedes first decision.", "endpoint-test"),
                jsonOptions),
            jsonOptions);
        Decision archived = await ReadOkAsync<Decision>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/{firstDecision.Id.Value}/archive",
                new ArchiveDecisionCommand("Archive superseded decision.", "endpoint-test"),
                jsonOptions),
            jsonOptions);

        Assert.Equal(DecisionCandidateState.Promoted, promoted.State);
        Assert.Equal(DecisionProposalState.Viewed, viewed.Proposal.State);
        Assert.Equal(DecisionReviewState.Viewed, viewed.Review.State);
        Assert.Equal(DecisionProposalState.NeedsRefinement, needsRefinement.Proposal.State);
        Assert.Equal(DecisionReviewState.NeedsRefinement, needsRefinement.Review.State);
        Assert.Equal(DecisionProposalState.Refined, refined.State);
        Assert.Equal("Refined context from lifecycle endpoint characterization.", refined.Context);
        Assert.Equal(DecisionProposalState.ReadyForResolution, ready.Proposal.State);
        Assert.Equal(DecisionState.Resolved, firstDecision.State);
        Assert.Equal(DecisionState.Resolved, replacementDecision.State);
        Assert.Equal(DecisionState.Superseded, superseded.State);
        Assert.Equal(DecisionState.Archived, archived.State);

        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        Decision? persistedFirst = await decisionRepository.GetDecisionAsync(repository, firstDecision.Id);
        Decision? persistedReplacement = await decisionRepository.GetDecisionAsync(repository, replacementDecision.Id);
        Assert.Equal(DecisionState.Archived, persistedFirst?.State);
        Assert.Contains(persistedReplacement!.Relationships, relationship =>
            relationship.Type == DecisionRelationshipType.Supersedes &&
            relationship.TargetDecisionId == firstDecision.Id);
    }

    [Fact]
    public async Task CoreLifecycleManagementEndpointsCoverTerminalCandidateAndProposalRoutes()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, "CAND-0001", DecisionCandidateState.Discovered));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, "CAND-0002", DecisionCandidateState.Discovered));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, "CAND-0003", DecisionCandidateState.Discovered));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, "CAND-0004", DecisionCandidateState.Promoted));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, "CAND-0005", DecisionCandidateState.Promoted));
        await using WebApplication app = await CreateApp(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionCandidate dismissed = await ReadOkAsync<DecisionCandidate>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-0001/dismiss",
                new DecisionCandidateTransitionRequest("Dismiss through endpoint."),
                jsonOptions),
            jsonOptions);
        DecisionCandidate expiredCandidate = await ReadOkAsync<DecisionCandidate>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-0002/expire",
                new DecisionCandidateTransitionRequest("Expire through endpoint."),
                jsonOptions),
            jsonOptions);
        DecisionCandidate duplicate = await ReadOkAsync<DecisionCandidate>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-0003/duplicate",
                new DecisionCandidateTransitionRequest("Duplicate through endpoint.", "CAND-0001"),
                jsonOptions),
            jsonOptions);
        DecisionProposal expiringProposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-0004/proposals",
                null),
            jsonOptions);
        DecisionProposal discardingProposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-0005/proposals",
                null),
            jsonOptions);
        DecisionProposal expiredProposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{expiringProposal.Id}/expire",
                new DecisionProposalTransitionRequest("Expire proposal through endpoint."),
                jsonOptions),
            jsonOptions);
        DecisionProposal discardedProposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{discardingProposal.Id}/discard",
                new DecisionProposalTransitionRequest("Discard proposal through endpoint."),
                jsonOptions),
            jsonOptions);

        Assert.Equal(DecisionCandidateState.Dismissed, dismissed.State);
        Assert.Equal(DecisionCandidateState.Expired, expiredCandidate.State);
        Assert.Equal(DecisionCandidateState.Duplicate, duplicate.State);
        Assert.Contains(duplicate.History.SelectMany(entry => entry.Sources), source => source.CandidateId == "CAND-0001");
        Assert.Equal(DecisionProposalState.Expired, expiredProposal.State);
        Assert.Equal(DecisionProposalState.Discarded, discardedProposal.State);
    }

    private static async Task<Decision> ResolveReplacementDecisionAsync(
        string root,
        HttpClient client,
        JsonSerializerOptions jsonOptions,
        Repository repository)
    {
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveCandidateAsync(
            repository,
            CreateCandidate(repository.Id, "CAND-9999", DecisionCandidateState.Promoted));
        DecisionProposal replacementProposal = await ReadOkAsync<DecisionProposal>(
            await client.PostAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/candidates/CAND-9999/proposals",
                null),
            jsonOptions);
        await ReadOkAsync<DecisionReviewWorkspace>(
            await client.PostAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{replacementProposal.Id}/review/ready-for-resolution",
                null),
            jsonOptions);
        return await ReadOkAsync<Decision>(
            await client.PostAsJsonAsync(
                $"{root}/api/repositories/{repository.Id}/decisions/proposals/{replacementProposal.Id}/resolve",
                new ResolveDecisionCommand("Resolve replacement through endpoint.", "endpoint-test", "option-1"),
                jsonOptions),
            jsonOptions);
    }

    private static async Task<T> ReadOkAsync<T>(HttpResponseMessage response, JsonSerializerOptions jsonOptions)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        T? value = JsonSerializer.Deserialize<T>(body, jsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private static async Task<WebApplication> CreateApp(Repository repository)
    {
        WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        return app;
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        string candidateId,
        DecisionCandidateState state)
    {
        string summary = $"Need to decide endpoint lifecycle coverage for {candidateId}.";
        return new DecisionCandidate(
            candidateId,
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            $"Endpoint lifecycle coverage {candidateId}",
            summary,
            $"source-fingerprint-{candidateId}",
            [new DecisionSignal(
                "MissingDirection",
                summary,
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [new DecisionEvidence(
                    "Endpoint lifecycle coverage needs a decision.",
                    [new DecisionSourceReference(
                        "Test",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: candidateId,
                        Excerpt: summary)])])],
            [new DecisionEvidence(
                "Endpoint lifecycle coverage needs a decision.",
                [new DecisionSourceReference(
                    "Test",
                    ".agents/plan.md",
                    Section: "Plan",
                    ItemId: candidateId,
                    Excerpt: summary)])],
            [new DecisionSourceReference(
                "Test",
                ".agents/plan.md",
                Section: "Plan",
                ItemId: candidateId,
                Excerpt: summary)],
            ["Created by endpoint lifecycle test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                state.ToString(),
                null,
                state.ToString(),
                "Seeded by endpoint lifecycle test.",
                [])]);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
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

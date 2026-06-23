using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionReasoningCaptureServiceTests
{
    [Fact]
    public async Task GovernanceContradictionCaptureIsIdempotentAndSelective()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        var captureService = new DecisionReasoningCaptureService(
            new StubRepositoryService(repository),
            decisionRepository,
            reasoningRepository);
        DecisionGovernanceReport report = CreateGovernanceReport(repository.Id);

        await captureService.CaptureGovernanceContradictionsAsync(repository.Id, report);
        await captureService.CaptureGovernanceContradictionsAsync(repository.Id, report);

        ReasoningEvent reasoningEvent = Assert.Single(await reasoningRepository.ListEventsAsync(repository));
        Assert.Equal(ReasoningEventFamily.Contradiction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.ContradictionIdentified, reasoningEvent.Type);
        Assert.Equal("InferredGovernanceContradiction", reasoningEvent.Provenance.SourceKind);
        Assert.Contains("Governance remains advisory", reasoningEvent.Narrative.Details);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.GovernanceFinding &&
            reference.Id == "GOV-0001" &&
            reference.RelativePath == ".agents/decisions/governance/governance.202606230000000000000.json");
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0001");
        Assert.DoesNotContain(reasoningEvent.Title, "Promoted candidate");
    }

    [Fact]
    public async Task GovernanceReportEndpointCapturesContradictionReasoningAfterReportPersists()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        Decision first = CreateResolvedDecision(repository.Id);
        Decision second = CreateResolvedDecision(repository.Id, "DEC-0002", "Use local-only authority") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.ConflictsWith,
                    "Directions cannot both be active.")
            ]
        };
        await decisionRepository.SaveDecisionAsync(repository, first);
        await decisionRepository.SaveDecisionAsync(repository, second);
        await ProjectAllAsync(repository, decisionRepository, store);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage response = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/governance/reports",
            null);
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ReasoningEvent reasoningEvent = Assert.Single(reasoningEvents, reasoningEvent =>
            reasoningEvent.Family == ReasoningEventFamily.Contradiction &&
            reasoningEvent.Type == ReasoningEventType.ContradictionIdentified &&
            reasoningEvent.Title.Contains("Conflicting resolved decisions", StringComparison.Ordinal));
        Assert.Equal("InferredGovernanceContradiction", reasoningEvent.Provenance.SourceKind);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.GovernanceFinding);
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0001");
        Assert.Contains(reasoningEvent.References, reference =>
            reference.Kind == ReasoningReferenceKind.Decision &&
            reference.Id == "DEC-0002");
    }

    [Fact]
    public async Task CurrentGovernanceReadDoesNotCaptureReasoning()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage governanceResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/governance");
        ReasoningEvent[] reasoningEvents = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events"))
            .Content.ReadFromJsonAsync<ReasoningEvent[]>(jsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, governanceResponse.StatusCode);
        Assert.Empty(reasoningEvents);
    }

    private static DecisionGovernanceReport CreateGovernanceReport(Guid repositoryId)
    {
        return new DecisionGovernanceReport(
            "governance.202606230000000000000",
            repositoryId,
            DateTimeOffset.Parse("2026-06-23T00:00:00Z"),
            "governance-input",
            DecisionHealthAssessment.Blocked,
            new DecisionGovernanceSummary(0, 0, 0, 0, 0, 2, 1),
            [
                new DecisionGovernanceFinding(
                    "GOV-0001",
                    DecisionGovernanceCategory.Consistency,
                    DecisionGovernanceSeverity.Blocking,
                    true,
                    "Conflicting resolved decisions",
                    "Resolved decisions conflict with one another.",
                    [new DecisionSourceReference("Decision", ".agents/decisions/records/DEC-0001/decision.json", DecisionId: new DecisionId("DEC-0001"))],
                    ["DEC-0001", "DEC-0002"],
                    [],
                    []),
                new DecisionGovernanceFinding(
                    "GOV-0002",
                    DecisionGovernanceCategory.DecisionCoverage,
                    DecisionGovernanceSeverity.Warning,
                    false,
                    "Promoted candidate has no proposal",
                    "Candidate CAND-0001 is promoted but has no active or resolved proposal.",
                    [new DecisionSourceReference("DecisionCandidate", ".agents/decisions/candidates/CAND-0001/candidate.json", CandidateId: "CAND-0001")],
                    [],
                    ["CAND-0001"],
                    [])
            ],
            []);
    }

    private static Decision CreateResolvedDecision(
        Guid repositoryId,
        string decisionId = "DEC-0001",
        string title = "Use repository-backed decisions")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var id = new DecisionId(decisionId);
        var proposal = new DecisionProposal(
            $"PROP-{decisionId[^4..]}",
            repositoryId,
            $"CAND-{decisionId[^4..]}",
            DecisionProposalState.ReadyForResolution,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            [new DecisionOption("option-1", title, "Persist records under .agents/decisions.", [])],
            [new DecisionTradeoff("option-1", "Recoverable.", "Requires schema discipline.", [])],
            new DecisionRecommendation("option-1", "Matches repository authority.", []),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            []);
        var snapshot = new DecisionResolvedProposalSnapshot(
            proposal.Id,
            proposal.CandidateId,
            Fingerprint(proposal),
            proposal.State,
            proposal.Title,
            proposal.Context,
            proposal.Options,
            proposal.Tradeoffs,
            proposal.Recommendation,
            proposal.Assumptions,
            proposal.Evidence,
            proposal.History,
            []);
        return new Decision(
            id,
            DecisionState.Resolved,
            DecisionClassification.Architectural,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            new DecisionMetadata(repositoryId, now, now),
            new DecisionResolution(
                DecisionOutcome.Accepted,
                "option-1",
                "Repository artifacts are the authoritative source.",
                "human-reviewer",
                false,
                now,
                [new DecisionSourceReference("DecisionProposal", $".agents/decisions/proposals/{proposal.Id}/proposal.json", DecisionId: id, ProposalId: proposal.Id)],
                snapshot),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(now, "Resolved", DecisionState.Open.ToString(), DecisionState.Resolved.ToString(), "Resolved by test.", [])]);
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, CreateJsonOptions()));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static async Task ProjectAllAsync(
        Repository repository,
        FileSystemDecisionRepository decisionRepository,
        FileSystemArtifactStore store)
    {
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        await projectionService.RefreshAllAsync(repository);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
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

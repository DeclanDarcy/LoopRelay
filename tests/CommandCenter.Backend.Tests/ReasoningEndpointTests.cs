using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Endpoints;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class ReasoningEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task ReasoningEndpointsCreateListGetAppendAndRelateArtifacts()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage createEventResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events",
            EventCommand("Event one"));
        ReasoningEvent createdEvent = (await createEventResponse.Content.ReadFromJsonAsync<ReasoningEvent>(JsonOptions))!;
        HttpResponseMessage listEventsResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/reasoning/events");
        HttpResponseMessage getEventResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/reasoning/events/{createdEvent.Id}");

        HttpResponseMessage createThreadResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads",
            new CreateReasoningThreadCommand(
                "Strategy thread",
                ReasoningThreadTheme.StrategicMovement,
                "Tracks strategy emergence.",
                [],
                []));
        ReasoningThread createdThread = (await createThreadResponse.Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;
        HttpResponseMessage appendResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads/{createdThread.Id}/events",
            new AppendReasoningThreadEventRequest(createdEvent.Id));
        HttpResponseMessage listThreadsResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/reasoning/threads");
        HttpResponseMessage getThreadResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/reasoning/threads/{createdThread.Id}");

        ReasoningEvent secondEvent = (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events",
            EventCommand("Event two"))).Content.ReadFromJsonAsync<ReasoningEvent>(JsonOptions))!;
        HttpResponseMessage createRelationshipResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/relationships",
            new CreateReasoningRelationshipCommand(
                ReasoningRelationshipType.Supports,
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, createdEvent.Id),
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, secondEvent.Id),
                new ReasoningNarrative("The first event supports the second."),
                Provenance()));
        HttpResponseMessage listRelationshipsResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/reasoning/relationships");

        Assert.Equal(HttpStatusCode.OK, createEventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listEventsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getEventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createThreadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, appendResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listThreadsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getThreadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createRelationshipResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listRelationshipsResponse.StatusCode);

        ReasoningThread appendedThread = (await appendResponse.Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;
        Assert.Equal([createdEvent.Id], appendedThread.EventIds);
    }

    [Fact]
    public async Task ReasoningEndpointsReturnExpectedErrorStatusCodes()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage missingRepositoryResponse = await client.GetAsync(
            $"{root}/api/repositories/{Guid.NewGuid()}/reasoning/events");
        HttpResponseMessage missingEventResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events/EVT-9999");
        HttpResponseMessage invalidEventResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events/not-an-id");
        HttpResponseMessage invalidCreateResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events",
            EventCommand(""));
        ReasoningEvent source = (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/events",
            EventCommand("Source"))).Content.ReadFromJsonAsync<ReasoningEvent>(JsonOptions))!;
        HttpResponseMessage conflictResponse = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/relationships",
            new CreateReasoningRelationshipCommand(
                ReasoningRelationshipType.Supports,
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, "EVT-9999"),
                new ReasoningNarrative("Missing target."),
                Provenance()));

        Assert.Equal(HttpStatusCode.NotFound, missingRepositoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingEventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task ManualCaptureTemplatesExposeUserSuppliedEventClassifications()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage response = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/manual-captures/templates");

        ManualReasoningCaptureTemplate[] templates =
            (await response.Content.ReadFromJsonAsync<ManualReasoningCaptureTemplate[]>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(templates, template =>
            template.Kind == ReasoningManualCaptureKind.AlternativeRejected &&
            template.Family == ReasoningEventFamily.Alternative &&
            template.Type == ReasoningEventType.AlternativeRejected &&
            template.ProvenanceSourceKind == "UserSupplied");
        Assert.Contains(templates, template =>
            template.Kind == ReasoningManualCaptureKind.ContradictionResolved &&
            template.SuggestedThreadTheme == ReasoningThreadTheme.Conflict);
    }

    [Fact]
    public async Task ManualCapturePreservesAlternativeRejectedAndRevisitedInEventThread()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();
        ReasoningThread thread = (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads",
            new CreateReasoningThreadCommand(
                "Alternative path",
                ReasoningThreadTheme.PathConsidered,
                "Tracks the alternative.",
                [],
                []))).Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;

        ReasoningEvent introduced = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.AlternativeIntroduced,
            "Alternative introduced",
            [thread.Id]);
        ReasoningEvent rejected = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.AlternativeRejected,
            "Alternative rejected",
            [thread.Id]);
        ReasoningEvent revisited = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.AlternativeRevisited,
            "Alternative revisited",
            [thread.Id]);

        ReasoningThread reloadedThread = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads/{thread.Id}"))
            .Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;

        Assert.All([introduced, rejected, revisited], reasoningEvent =>
            Assert.Equal(ReasoningEventFamily.Alternative, reasoningEvent.Family));
        Assert.Equal(ReasoningEventType.AlternativeRejected, rejected.Type);
        Assert.Equal([introduced.Id, rejected.Id, revisited.Id], reloadedThread.EventIds);
    }

    [Fact]
    public async Task ManualCapturePreservesContradictionIdentifiedAndResolvedInEventThread()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();
        ReasoningThread thread = (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads",
            new CreateReasoningThreadCommand(
                "Contradiction",
                ReasoningThreadTheme.Conflict,
                "Tracks the contradiction.",
                [],
                []))).Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;

        ReasoningEvent identified = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.ContradictionIdentified,
            "Contradiction identified",
            [thread.Id]);
        ReasoningEvent resolved = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.ContradictionResolved,
            "Contradiction resolved",
            [thread.Id]);
        ReasoningThread reloadedThread = (await (await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/threads/{thread.Id}"))
            .Content.ReadFromJsonAsync<ReasoningThread>(JsonOptions))!;

        Assert.Equal(ReasoningEventFamily.Contradiction, identified.Family);
        Assert.Equal(ReasoningEventType.ContradictionResolved, resolved.Type);
        Assert.Equal("UserSupplied", resolved.Provenance.SourceKind);
        Assert.Equal([identified.Id, resolved.Id], reloadedThread.EventIds);
    }

    [Fact]
    public async Task ManualCaptureRecordsDirectionShiftWithoutMaterializedDirectionEntity()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();

        ReasoningEvent reasoningEvent = await ManualCaptureAsync(
            client,
            root,
            repository,
            ReasoningManualCaptureKind.DirectionShifted,
            "Direction shifted",
            []);

        Assert.Equal(ReasoningEventFamily.Direction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.DirectionShifted, reasoningEvent.Type);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "directions")));
    }

    [Fact]
    public async Task ManualCaptureRejectsInferredProvenance()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = await CreateAppAsync(repository);
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/manual-captures",
            new ManualReasoningCaptureCommand(
                ReasoningManualCaptureKind.HypothesisRaised,
                "Hypothesis raised",
                new ReasoningNarrative("A hypothesis was raised."),
                [],
                new ReasoningProvenance("InferredDecisionSupersession", "agent"),
                [],
                []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(Repository repository)
    {
        WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        return app;
    }

    private static CreateReasoningEventCommand EventCommand(string title)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            title,
            new ReasoningNarrative("A hypothesis was raised."),
            [],
            Provenance(),
            [],
            []);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent", ".agents/plan.md", "Milestone 1", "excerpt", "fingerprint");
    }

    private static async Task<ReasoningEvent> ManualCaptureAsync(
        HttpClient client,
        string root,
        Repository repository,
        ReasoningManualCaptureKind kind,
        string title,
        IReadOnlyList<string> threadIds)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/reasoning/manual-captures",
            new ManualReasoningCaptureCommand(
                kind,
                title,
                new ReasoningNarrative($"{title}."),
                [new ReasoningReference(ReasoningReferenceKind.Artifact, "manual-note", ".agents/plan.md")],
                new ReasoningProvenance("UserSupplied", "agent"),
                threadIds,
                ["manual-capture"]));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReasoningEvent>(JsonOptions))!;
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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

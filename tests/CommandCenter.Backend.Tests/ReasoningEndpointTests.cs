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

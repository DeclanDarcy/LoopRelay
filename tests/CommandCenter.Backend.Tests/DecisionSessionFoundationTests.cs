using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Primitives;
using CommandCenter.DecisionSessions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionFoundationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void SessionIdRoundTrips()
    {
        DecisionSessionId id = DecisionSessionId.New();

        Assert.Equal(id, DecisionSessionId.Parse(id.ToString()));
    }

    [Fact]
    public async Task CreateActivateAndRetireSession()
    {
        Harness harness = CreateHarness();

        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        DecisionSession retired = await harness.Registry.RetireSessionAsync(harness.Repository.Id, active.Id, "done");

        Assert.Equal(DecisionSessionState.Created, created.State);
        Assert.Equal(DecisionSessionState.Active, active.State);
        Assert.NotNull(active.ActivatedAt);
        Assert.Equal(DecisionSessionState.Retired, retired.State);
        Assert.NotNull(retired.RetiredAt);
        Assert.Null(await harness.Registry.GetActiveSessionAsync(harness.Repository.Id));
    }

    [Fact]
    public async Task ActivatingSecondSessionIsRejected()
    {
        Harness harness = CreateHarness();
        DecisionSession first = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession second = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, first.Id);

        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.ActivateSessionAsync(harness.Repository.Id, second.Id));
    }

    [Fact]
    public async Task RepositoryPersistsOrderedRegistry()
    {
        Harness harness = CreateHarness();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);

        IReadOnlyList<DecisionSession> sessions = await harness.RepositoryStore.ListAsync(harness.Repository);

        DecisionSession session = Assert.Single(sessions);
        Assert.Equal(created.Id, session.Id);
        Assert.Equal(DecisionSessionState.Active, session.State);
    }

    [Fact]
    public async Task DuplicateIdsAreRejected()
    {
        Harness harness = CreateHarness();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession first = DecisionSession.Create(harness.Repository.Id, "test", now);
        DecisionSession duplicate = first with { State = DecisionSessionState.Retired, RetiredAt = now.AddMinutes(1) };

        await WriteRegistryAsync(harness, [first, duplicate]);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("Duplicate decision session id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossRepositoryRegistryIsRejected()
    {
        Harness harness = CreateHarness();
        DecisionSession session = DecisionSession.Create(Guid.NewGuid(), "test", DateTimeOffset.UtcNow);

        await WriteRegistryAsync(harness, [session], session.RepositoryId);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("different repository", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndpointsReturnSessionsActiveAndDiagnostics()
    {
        Harness harness = CreateHarness();
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.RemoveAll<IArtifactStore>();
                services.RemoveAll<IRepositoryService>();
                services.AddSingleton<IArtifactStore>(harness.Store);
                services.AddSingleton<IRepositoryService>(harness.RepositoryService);
            });
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);

        DecisionSessionProjection[]? sessions = await client.GetFromJsonAsync<DecisionSessionProjection[]>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions",
            JsonOptions);
        DecisionSessionProjection? active = await client.GetFromJsonAsync<DecisionSessionProjection>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/active",
            JsonOptions);
        DecisionSessionDiagnostics? diagnostics = await client.GetFromJsonAsync<DecisionSessionDiagnostics>(
            $"{root}/api/repositories/{harness.Repository.Id}/decision-sessions/diagnostics",
            JsonOptions);

        Assert.NotNull(sessions);
        Assert.Single(sessions);
        Assert.NotNull(active);
        Assert.Equal(created.Id, active.Id);
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.IsValid);
    }

    [Fact]
    public async Task MissingRepositoryEndpointReturnsNotFound()
    {
        Harness harness = CreateHarness();
        await using WebApplication app = Program.CreateApp(
            [],
            services =>
            {
                services.RemoveAll<IArtifactStore>();
                services.RemoveAll<IRepositoryService>();
                services.AddSingleton<IArtifactStore>(harness.Store);
                services.AddSingleton<IRepositoryService>(harness.RepositoryService);
            });
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage response = await client.GetAsync($"{root}/api/repositories/{Guid.NewGuid()}/decision-sessions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Harness CreateHarness()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"))
        };
        Directory.CreateDirectory(repository.Path);
        var store = new MemoryArtifactStore();
        var repositoryService = new StubRepositoryService(repository);
        var sessionRepository = new FileSystemDecisionSessionRepository(store);
        var registry = new DecisionSessionRegistry(repositoryService, sessionRepository);
        var recovery = new DecisionSessionRecoveryService(repositoryService, sessionRepository);
        return new Harness(repository, store, repositoryService, sessionRepository, registry, recovery);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task WriteRegistryAsync(
        Harness harness,
        IReadOnlyList<DecisionSession> sessions,
        Guid? documentRepositoryId = null,
        string schemaVersion = DecisionSessionArtifactPaths.SchemaVersion)
    {
        var document = new DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>(
            schemaVersion,
            documentRepositoryId ?? harness.Repository.Id,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            sessions.Select(session => new DecisionSessionRecord(session)).ToArray());
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.RegistryJson()),
            System.Text.Json.JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    private sealed record Harness(
        Repository Repository,
        MemoryArtifactStore Store,
        StubRepositoryService RepositoryService,
        FileSystemDecisionSessionRepository RepositoryStore,
        DecisionSessionRegistry Registry,
        DecisionSessionRecoveryService Recovery);

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

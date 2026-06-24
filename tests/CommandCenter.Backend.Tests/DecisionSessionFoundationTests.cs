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
    public void StateEnumRoundTripsThroughJson()
    {
        string json = JsonSerializer.Serialize(DecisionSessionState.TransferPending, DecisionSessionJson.Options);

        DecisionSessionState state = JsonSerializer.Deserialize<DecisionSessionState>(json, DecisionSessionJson.Options);

        Assert.Equal("\"TransferPending\"", json);
        Assert.Equal(DecisionSessionState.TransferPending, state);
    }

    [Fact]
    public void AggregateCreationSetsRepositoryOwnership()
    {
        Guid repositoryId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        DecisionSession session = DecisionSession.Create(repositoryId, "test", createdAt);

        Assert.Equal(repositoryId, session.RepositoryId);
        Assert.Equal(DecisionSessionState.Created, session.State);
        Assert.Equal(createdAt, session.CreatedAt);
        Assert.Equal(repositoryId, session.Ownership.RepositoryId);
        Assert.Equal("test", session.Ownership.CreatedBy);
        Assert.Equal(createdAt, session.Ownership.CreatedAt);
        Assert.Null(session.ActivatedAt);
        Assert.Null(session.RetiredAt);
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
    public async Task ZeroAndOneActiveSessionsAreAllowed()
    {
        Harness harness = CreateHarness();

        Assert.Null(await harness.Registry.GetActiveSessionAsync(harness.Repository.Id));

        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);

        DecisionSession? resolved = await harness.Registry.GetActiveSessionAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionState.Active, active.State);
        Assert.NotNull(resolved);
        Assert.Equal(active.Id, resolved.Id);
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
    public async Task TransferPendingAndTransferredTransitionsAreEnforced()
    {
        Harness harness = CreateHarness();
        DecisionSession source = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession activeSource = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, source.Id);
        DecisionSession pending = await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, activeSource.Id, "pressure");
        DecisionSession target = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession activeTarget = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, target.Id);

        DecisionSession transferred = await harness.Registry.MarkTransferredAsync(
            harness.Repository.Id,
            pending.Id,
            activeTarget.Id,
            "completed");

        Assert.Equal(DecisionSessionState.TransferPending, pending.State);
        Assert.Equal("pressure", pending.Metadata.TransferReason);
        Assert.Equal(DecisionSessionState.Transferred, transferred.State);
        Assert.NotNull(transferred.RetiredAt);
        Assert.Equal(activeTarget.Id, transferred.Metadata.TransferredToSessionId);
        Assert.Equal(activeTarget.Id, (await harness.Registry.GetActiveSessionAsync(harness.Repository.Id))?.Id);
    }

    [Fact]
    public async Task InvalidRegistryTransitionsAreRejected()
    {
        Harness harness = CreateHarness();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");

        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, created.Id, "not active"));
        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.RetireSessionAsync(harness.Repository.Id, created.Id, "not active"));

        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.ActivateSessionAsync(harness.Repository.Id, active.Id));
        DecisionSession pending = await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, active.Id, "pressure");
        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.ActivateSessionAsync(harness.Repository.Id, pending.Id));

        DecisionSession retired = await harness.Registry.RetireSessionAsync(harness.Repository.Id, pending.Id, "done");
        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.ActivateSessionAsync(harness.Repository.Id, retired.Id));
        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.RetireSessionAsync(harness.Repository.Id, retired.Id, "again"));
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
    public async Task RepositoryRejectsWrongOwnershipOnWrite()
    {
        Harness harness = CreateHarness();
        DecisionSession session = DecisionSession.Create(Guid.NewGuid(), "test", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.RepositoryStore.CreateAsync(harness.Repository, session));
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
    public async Task InvalidTimestampStateProducesDiagnostics()
    {
        Harness harness = CreateHarness();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession session = DecisionSession.Create(harness.Repository.Id, "test", now) with
        {
            State = DecisionSessionState.Retired,
            ActivatedAt = now.AddMinutes(2),
            RetiredAt = now.AddMinutes(1)
        };

        await WriteRegistryAsync(harness, [session]);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("activation is after retirement", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnsupportedSchemaVersionIsRejected()
    {
        Harness harness = CreateHarness();
        DecisionSession session = DecisionSession.Create(harness.Repository.Id, "test", DateTimeOffset.UtcNow);

        await WriteRegistryAsync(harness, [session], schemaVersion: "decision-sessions.v0");

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("Unsupported decision session schema version", StringComparison.Ordinal));
        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            harness.RepositoryStore.ListAsync(harness.Repository));
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

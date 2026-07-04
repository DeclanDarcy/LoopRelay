using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;

namespace CommandCenter.DecisionSessions.Tests;

public sealed class DecisionSessionRepositoryTests
{
    [Fact]
    public async Task RepositoryPersistsOrderedRegistry()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
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
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession session = DecisionSession.Create(Guid.NewGuid(), "test", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.RepositoryStore.CreateAsync(harness.Repository, session));
    }

    [Fact]
    public async Task DuplicateIdsAreRejected()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession first = DecisionSession.Create(harness.Repository.Id, "test", now);
        DecisionSession duplicate = first with { State = DecisionSessionState.Retired, RetiredAt = now.AddMinutes(1) };

        await harness.WriteRegistryAsync([first, duplicate]);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("Duplicate decision session id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidTimestampStateProducesDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession session = DecisionSession.Create(harness.Repository.Id, "test", now) with
        {
            State = DecisionSessionState.Retired,
            ActivatedAt = now.AddMinutes(2),
            RetiredAt = now.AddMinutes(1)
        };

        await harness.WriteRegistryAsync([session]);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("activation is after retirement", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnsupportedSchemaVersionIsRejected()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession session = DecisionSession.Create(harness.Repository.Id, "test", DateTimeOffset.UtcNow);

        await harness.WriteRegistryAsync([session], schemaVersion: "decision-sessions.v0");

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("Unsupported decision session schema version", StringComparison.Ordinal));
        await Assert.ThrowsAsync<DecisionSessionValidationException>(() =>
            harness.RepositoryStore.ListAsync(harness.Repository));
    }

    [Fact]
    public async Task CrossRepositoryRegistryIsRejected()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession session = DecisionSession.Create(Guid.NewGuid(), "test", DateTimeOffset.UtcNow);

        await harness.WriteRegistryAsync([session], session.RepositoryId);

        DecisionSessionDiagnostics diagnostics = await harness.Recovery.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.False(diagnostics.IsValid);
        Assert.Contains(diagnostics.Errors, error => error.Contains("different repository", StringComparison.Ordinal));
    }
}

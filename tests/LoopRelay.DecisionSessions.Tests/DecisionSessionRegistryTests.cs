using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Tests;

public sealed class DecisionSessionRegistryTests
{
    [Fact]
    public async Task CreateActivateAndRetireSession()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();

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
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();

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
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession first = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession second = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, first.Id);

        await Assert.ThrowsAsync<DecisionSessionConflictException>(() =>
            harness.Registry.ActivateSessionAsync(harness.Repository.Id, second.Id));
    }

    [Fact]
    public async Task TransferPendingAndTransferredTransitionsAreEnforced()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
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
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
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
}

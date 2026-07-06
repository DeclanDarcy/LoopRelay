using System.Text.Json;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Tests;

public sealed class DecisionSessionFoundationTests
{
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
}

using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Services;

namespace LoopRelay.Agents.Tests.Services;

public sealed class AgentSessionRegistryTests
{
    [Fact]
    public async Task TryAddAndTryGetRoundTripsBySessionKey()
    {
        var registry = new AgentSessionRegistry();
        var key = new AgentSessionKey("repo-1", SessionIdentity.New());
        var session = new FakeAgentSession("repo-1");

        Assert.True(registry.TryAdd(key, session));
        Assert.False(registry.TryAdd(key, session));
        Assert.True(registry.TryGet(key, out IAgentSession? resolved));
        Assert.Same(session, resolved);

        await registry.DisposeAsync();
    }

    [Fact]
    public async Task ForRepositoryIsolatesByRepositoryId()
    {
        var registry = new AgentSessionRegistry();
        var a = new FakeAgentSession("repo-a");
        var b = new FakeAgentSession("repo-b");
        registry.TryAdd(new AgentSessionKey("repo-a", SessionIdentity.New()), a);
        registry.TryAdd(new AgentSessionKey("repo-b", SessionIdentity.New()), b);

        IReadOnlyList<IAgentSession> forA = registry.ForRepository("repo-a");

        Assert.Single(forA);
        Assert.Same(a, forA[0]);

        await registry.DisposeAsync();
    }

    [Fact]
    public async Task RemoveDisposesSessionAndDropsKey()
    {
        var registry = new AgentSessionRegistry();
        var key = new AgentSessionKey("repo-1", SessionIdentity.New());
        var session = new FakeAgentSession("repo-1");
        registry.TryAdd(key, session);

        Assert.True(await registry.RemoveAsync(key));

        Assert.True(session.Disposed);
        Assert.False(registry.TryGet(key, out _));
        Assert.False(await registry.RemoveAsync(key));
    }

    [Fact]
    public async Task DisposeRepositoryDisposesOnlyThatRepositorysSessions()
    {
        var registry = new AgentSessionRegistry();
        var a1 = new FakeAgentSession("repo-a");
        var a2 = new FakeAgentSession("repo-a");
        var b = new FakeAgentSession("repo-b");
        registry.TryAdd(new AgentSessionKey("repo-a", SessionIdentity.New()), a1);
        registry.TryAdd(new AgentSessionKey("repo-a", SessionIdentity.New()), a2);
        registry.TryAdd(new AgentSessionKey("repo-b", SessionIdentity.New()), b);

        await registry.DisposeRepositoryAsync("repo-a");

        Assert.True(a1.Disposed);
        Assert.True(a2.Disposed);
        Assert.False(b.Disposed);
        Assert.Equal(1, registry.Count);

        await registry.DisposeAsync();
        Assert.True(b.Disposed);
    }

    private sealed class FakeAgentSession(string repositoryId) : IAgentSession
    {
        public bool Disposed { get; private set; }

        public SessionIdentity SessionId { get; } = SessionIdentity.New();

        public string RepositoryId => repositoryId;

        public SessionRole Role => SessionRole.Decision;

        public AgentSessionMode Mode => AgentSessionMode.Persistent;

        public AgentProcessState State => Disposed ? AgentProcessState.Disposed : AgentProcessState.Running;

        public int CompletedTurns => 0;

        public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;

        public string? ThreadId => null;

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentTurnResult(1, AgentTurnState.Completed, string.Empty, AgentTokenUsage.Zero));

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

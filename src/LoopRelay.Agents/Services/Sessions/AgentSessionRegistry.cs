using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Services.Sessions;

/// <summary>
/// Owns live agent sessions keyed by repository id and session id so a warm process can be
/// reused across HTTP calls and disposed deterministically on repository or application shutdown.
/// </summary>
public sealed class AgentSessionRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<AgentSessionKey, IAgentSession> sessions = new();

    public int Count => sessions.Count;

    public bool TryAdd(AgentSessionKey key, IAgentSession session) =>
        sessions.TryAdd(key, session);

    public bool TryGet(AgentSessionKey key, [MaybeNullWhen(false)] out IAgentSession session) =>
        sessions.TryGetValue(key, out session);

    public IReadOnlyList<IAgentSession> ForRepository(string repositoryId) =>
        sessions
            .Where(pair => string.Equals(pair.Key.RepositoryId, repositoryId, StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToArray();

    public async Task<bool> RemoveAsync(AgentSessionKey key)
    {
        if (sessions.TryRemove(key, out IAgentSession? session))
        {
            await session.DisposeAsync();
            return true;
        }

        return false;
    }

    public async Task DisposeRepositoryAsync(string repositoryId)
    {
        foreach (AgentSessionKey key in sessions.Keys
                     .Where(key => string.Equals(key.RepositoryId, repositoryId, StringComparison.Ordinal))
                     .ToArray())
        {
            await RemoveAsync(key);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (AgentSessionKey key in sessions.Keys.ToArray())
        {
            await RemoveAsync(key);
        }
    }
}

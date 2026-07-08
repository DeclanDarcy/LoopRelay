namespace LoopRelay.Agents.Models;

public readonly record struct AgentSessionKey(string RepositoryId, SessionIdentity SessionId);

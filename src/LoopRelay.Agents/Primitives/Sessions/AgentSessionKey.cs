namespace LoopRelay.Agents.Primitives;

public readonly record struct AgentSessionKey(string RepositoryId, SessionIdentity SessionId);

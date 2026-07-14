namespace LoopRelay.Agents.Primitives.Sessions;

public readonly record struct AgentSessionKey(string RepositoryId, SessionIdentity SessionId);

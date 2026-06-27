namespace CommandCenter.Agents.Models;

public sealed record AgentStreamChunk(
    int TurnIndex,
    AgentProcessOutputStream Stream,
    string Content);

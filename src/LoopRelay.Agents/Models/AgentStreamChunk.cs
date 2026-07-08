using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Models;

public sealed record AgentStreamChunk(
    int TurnIndex,
    AgentProcessOutputStream Stream,
    string Content,
    AgentStreamChunkKind Kind = AgentStreamChunkKind.AgentMessage);

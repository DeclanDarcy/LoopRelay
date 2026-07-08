using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Streams;

namespace LoopRelay.Agents.Models.Streams;

public sealed record AgentStreamChunk(
    int TurnIndex,
    AgentProcessOutputStream Stream,
    string Content,
    AgentStreamChunkKind Kind = AgentStreamChunkKind.AgentMessage);

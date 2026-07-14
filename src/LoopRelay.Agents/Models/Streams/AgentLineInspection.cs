using LoopRelay.Agents.Primitives.Streams;

namespace LoopRelay.Agents.Models.Streams;

public sealed record AgentLineInspection(
    AgentLineClassification Classification,
    AgentTokenUsage? Usage = null,
    string? Content = null,
    string? StreamId = null);

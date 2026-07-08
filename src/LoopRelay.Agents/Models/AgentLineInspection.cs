namespace LoopRelay.Agents.Models;

public sealed record AgentLineInspection(
    AgentLineClassification Classification,
    AgentTokenUsage? Usage = null,
    string? Content = null,
    string? StreamId = null);

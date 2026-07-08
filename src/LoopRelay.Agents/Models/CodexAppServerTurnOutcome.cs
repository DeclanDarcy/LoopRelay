using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Models;

/// <summary>The accumulated result of one Codex app-server turn.</summary>
public sealed record CodexAppServerTurnOutcome(
    string Output,
    AgentTokenUsage? Usage,
    AgentTurnState State,
    string? FailureMessage);

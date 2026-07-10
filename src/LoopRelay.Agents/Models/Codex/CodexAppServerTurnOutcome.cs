using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Models.Codex;

/// <summary>The accumulated result of one Codex app-server turn.</summary>
public sealed record CodexAppServerTurnOutcome(
    string Output,
    AgentTokenUsage? Usage,
    AgentTurnState State,
    string? FailureMessage,
    string? ProviderTurnId);

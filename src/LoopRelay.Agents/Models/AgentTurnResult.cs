namespace LoopRelay.Agents.Models;

/// <summary>
/// The outcome of one agent turn. <paramref name="Diagnostics"/> is failure-only context — the
/// retained tail of the process's standard error when a one-shot exited nonzero (null otherwise),
/// so the operator sees WHY codex refused/failed instead of a bare state.
/// </summary>
public sealed record AgentTurnResult(
    int TurnIndex,
    AgentTurnState State,
    string Output,
    AgentTokenUsage Usage,
    string? Diagnostics = null);

namespace LoopRelay.Agents.Primitives;

/// <summary>One live surfacing from a turn: either an agent-reply delta or a compact tool-call summary.</summary>
public readonly record struct CodexStreamEmission(string Text, AgentStreamChunkKind Kind);

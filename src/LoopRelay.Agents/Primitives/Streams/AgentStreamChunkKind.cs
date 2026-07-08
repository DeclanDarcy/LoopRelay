namespace LoopRelay.Agents.Primitives;

/// <summary>
/// What a streamed chunk represents. Agent-reply text (the accumulated turn Output) is surfaced as
/// <see cref="AgentMessage"/>; a tool invocation the agent ran mid-turn (a shell command, file edit,
/// MCP/web call) is surfaced as a distinct <see cref="ToolCall"/> so a console can render it compactly
/// and, crucially, never fold it into the reply text.
/// </summary>
public enum AgentStreamChunkKind
{
    AgentMessage,
    ToolCall
}

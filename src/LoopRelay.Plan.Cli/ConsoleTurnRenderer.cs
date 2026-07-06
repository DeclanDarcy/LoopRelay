using LoopRelay.Agents.Models;

namespace LoopRelay.Plan.Cli;

/// <summary>
/// Renders one codex turn's live stream to the console and de-duplicates its final echo. A turn's reply streams
/// in as <see cref="AgentStreamChunkKind.AgentMessage"/> deltas (printed inline) interleaved with
/// <see cref="AgentStreamChunkKind.ToolCall"/> summaries (printed as compact lines). Because the reply is already
/// shown live, <see cref="EchoIfSilent"/> reprints the full <c>Output</c> ONLY when nothing streamed — the
/// fallback path where codex delivered the reply as a single completed item rather than deltas. One renderer per
/// turn (its streamed-state is per-turn).
/// </summary>
internal sealed class ConsoleTurnRenderer(ILoopConsole console)
{
    private bool replyStreamed;

    public Task Stream(AgentStreamChunk chunk)
    {
        if (chunk.Stream != AgentProcessOutputStream.StandardOutput)
        {
            return Task.CompletedTask;
        }

        if (chunk.Kind == AgentStreamChunkKind.ToolCall)
        {
            console.Tool(chunk.Content);
        }
        else
        {
            console.Delta(chunk.Content);
            replyStreamed = true;
        }

        return Task.CompletedTask;
    }

    // Print the turn's full reply only if it never streamed live, so a streamed reply is not shown twice. Tool
    // calls don't count as a streamed reply (they aren't part of Output), so a tool-only turn still echoes.
    public void EchoIfSilent(string output)
    {
        if (!replyStreamed)
        {
            console.Message(output);
        }
    }
}

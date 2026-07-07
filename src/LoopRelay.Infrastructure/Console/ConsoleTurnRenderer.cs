using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Console;

/// <summary>Renders a single agent turn stream and suppresses duplicate final echoes.</summary>
public class ConsoleTurnRenderer(ILoopConsole console)
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
            AgentTurnProgress.Notify(observer => observer.FirstProtocolEvent());
            console.Tool(chunk.Content);
        }
        else
        {
            AgentTurnProgress.Notify(observer => observer.FirstProtocolEvent());
            AgentTurnProgress.Notify(observer => observer.FirstOutput());
            console.Delta(chunk.Content);
            replyStreamed = true;
        }

        return Task.CompletedTask;
    }

    public void EchoIfSilent(string output)
    {
        if (!replyStreamed)
        {
            console.Message(output);
        }
    }
}

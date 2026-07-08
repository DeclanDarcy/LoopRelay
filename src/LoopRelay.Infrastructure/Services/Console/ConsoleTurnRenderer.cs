using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Services;
using LoopRelay.Infrastructure.Abstractions.Console;

namespace LoopRelay.Infrastructure.Services.Console;

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
            AgentTurnProgress.Notify(observer => observer.FirstOutput());
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

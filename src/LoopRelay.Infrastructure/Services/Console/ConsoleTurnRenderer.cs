using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Streams;
using LoopRelay.Agents.Services;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Infrastructure.Abstractions.Console;

namespace LoopRelay.Infrastructure.Services.Console;

/// <summary>Renders a single agent turn stream and suppresses duplicate final echoes.</summary>
public class ConsoleTurnRenderer(ILoopConsole _console)
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
            _console.Tool(chunk.Content);
        }
        else
        {
            AgentTurnProgress.Notify(observer => observer.FirstProtocolEvent());
            AgentTurnProgress.Notify(observer => observer.FirstOutput());
            _console.Delta(chunk.Content);
            replyStreamed = true;
        }

        return Task.CompletedTask;
    }

    public void EchoIfSilent(string output)
    {
        if (!replyStreamed)
        {
            _console.Message(output);
        }
    }
}

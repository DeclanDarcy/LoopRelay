using CommandCenter.Agents.Models;

namespace CommandCenter.Roadmap.Cli;

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

    public void EchoIfSilent(string output)
    {
        if (!replyStreamed)
        {
            console.Message(output);
        }
    }
}

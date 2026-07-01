using System.IO;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class ConsoleTurnRendererTests
{
    private static (ConsoleTurnRenderer Renderer, StringWriter Out) New()
    {
        var outw = new StringWriter { NewLine = "\n" };
        var console = new ConsoleLoopConsole(outw, new StringWriter { NewLine = "\n" });
        return (new ConsoleTurnRenderer(console), outw);
    }

    private static AgentStreamChunk Reply(string text) =>
        new(0, AgentProcessOutputStream.StandardOutput, text, AgentStreamChunkKind.AgentMessage);

    private static AgentStreamChunk ToolCall(string text) =>
        new(0, AgentProcessOutputStream.StandardOutput, text, AgentStreamChunkKind.ToolCall);

    // The core de-dup: a reply that streamed live must NOT be reprinted by EchoIfSilent.
    [Fact]
    public async Task StreamedReply_IsNotEchoedAgain()
    {
        var (renderer, outw) = New();

        await renderer.Stream(Reply("hello world"));
        renderer.EchoIfSilent("hello world");

        Assert.Equal("hello world", outw.ToString()); // shown once (the streamed copy), not twice
    }

    // The fallback: when nothing streamed (codex delivered one completed item), the reply IS echoed.
    [Fact]
    public void SilentTurn_EchoesTheFullOutput()
    {
        var (renderer, outw) = New();

        renderer.EchoIfSilent("full reply");

        Assert.Equal("full reply\n", outw.ToString());
    }

    // A tool call is not a streamed reply, so a turn that only ran tools still echoes its (fallback) reply.
    [Fact]
    public async Task ToolCallDoesNotSuppressTheEcho()
    {
        var (renderer, outw) = New();

        await renderer.Stream(ToolCall("$ git status"));
        renderer.EchoIfSilent("the reply");

        Assert.Equal("  $ git status\nthe reply\n", outw.ToString());
    }

    // Tool calls interleave with the streamed reply as compact lines; the reply is not echoed again.
    [Fact]
    public async Task ToolCallsAndReplyInterleave_WithNoDuplicateEcho()
    {
        var (renderer, outw) = New();

        await renderer.Stream(ToolCall("$ dotnet build"));
        await renderer.Stream(Reply("Built and done."));
        renderer.EchoIfSilent("Built and done.");

        Assert.Equal("  $ dotnet build\nBuilt and done.", outw.ToString());
    }
}

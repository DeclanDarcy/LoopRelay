using LoopRelay.Agents.Models.Codex;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Primitives.Streams;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexAppServerTurnReaderTests
{
    private static CodexAppServerMessage Msg(string json) => CodexAppServerMessage.Parse(json);

    [Fact]
    public void AccumulatesAgentMessageDeltasRealUsageAndCompletion()
    {
        var reader = new CodexAppServerTurnReader();

        reader.Apply(Msg("""{"method":"turn/started","params":{"threadId":"T","turn":{"id":"u1","status":"inProgress"}}}"""));
        CodexStreamEmission? delta = reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"threadId":"T","turnId":"u1","itemId":"i1","delta":"Hel"}}"""));
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"threadId":"T","turnId":"u1","itemId":"i1","delta":"lo"}}"""));
        reader.Apply(Msg("""{"method":"thread/tokenUsage/updated","params":{"threadId":"T","turnId":"u1","tokenUsage":{"last":{"totalTokens":37,"inputTokens":30,"cachedInputTokens":1,"outputTokens":7,"reasoningOutputTokens":0}}}}"""));
        Assert.False(reader.IsComplete);
        reader.Apply(Msg("""{"method":"turn/completed","params":{"threadId":"T","turn":{"id":"u1","status":"completed"}}}"""));

        Assert.Equal("Hel", delta!.Value.Text); // returned for live streaming
        Assert.Equal(AgentStreamChunkKind.AgentMessage, delta.Value.Kind);
        Assert.True(reader.IsComplete);

        CodexAppServerTurnOutcome outcome = reader.Result();
        Assert.Equal("Hello", outcome.Output); // deltas concatenate into the reply
        Assert.Equal(AgentTurnState.Completed, outcome.State);
        Assert.Equal(30, outcome.Usage!.PromptTokens);
        Assert.Equal(7, outcome.Usage.OutputTokens);
        Assert.Equal(1, outcome.Usage.CachedInputTokens); // the cached subset of the 30 input tokens (cost-aware routing signal)
    }

    [Fact]
    public void DeltasAreTheOutputAndACompletedItemDoesNotDoubleCount()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i1","delta":"answer"}}"""));
        // A trailing completed item carrying the same full text must not be appended again.
        reader.Apply(Msg("""{"method":"item/completed","params":{"item":{"type":"agentMessage","text":"answer"}}}"""));

        Assert.Equal("answer", reader.Result().Output);
    }

    [Fact]
    public void FailedTurnSurfacesStateAndError()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"turn/completed","params":{"turn":{"status":"failed","error":{"message":"model unavailable"}}}}"""));

        CodexAppServerTurnOutcome outcome = reader.Result();
        Assert.Equal(AgentTurnState.Failed, outcome.State);
        Assert.Equal("model unavailable", outcome.FailureMessage);
    }

    [Fact]
    public void InterruptedTurnMapsToCanceled()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"turn/completed","params":{"turn":{"status":"interrupted"}}}"""));

        Assert.Equal(AgentTurnState.Canceled, reader.Result().State);
    }

    [Fact]
    public void CompletedAgentItemIsFallbackWhenNoDeltasAndResponsesAreIgnored()
    {
        var reader = new CodexAppServerTurnReader();

        // A response (ack) must not affect turn accumulation.
        Assert.Null(reader.Apply(Msg("""{"id":3,"result":{"turn":{"id":"u1"}}}""")));
        // No deltas arrived, so the completed agent item supplies the text.
        reader.Apply(Msg("""{"method":"item/completed","params":{"item":{"type":"agent_message","text":"fallback reply"}}}"""));

        Assert.Equal("fallback reply", reader.Result().Output);
    }

    [Fact]
    public void ReasoningItemsAreNotTreatedAsAgentOutput()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/completed","params":{"item":{"type":"reasoning","text":"thinking"}}}"""));

        Assert.Equal(string.Empty, reader.Result().Output);
    }

    // Codex narrates a long turn (e.g. execution) as SEPARATE agent-message items; without a separator their
    // deltas concatenate into one run-on blob. A new item's first delta after prior output inserts a newline so
    // each message lands on its own line — in the accumulated Output AND in the live delta surfaced to the console.
    [Fact]
    public void ConsecutiveAgentMessages_AreSeparatedByANewline()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i1","delta":"first message"}}"""));
        CodexStreamEmission? second = reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i2","delta":"second message"}}"""));

        Assert.Equal("\nsecond message", second!.Value.Text); // the live chunk carries the break
        Assert.Equal("first message\nsecond message", reader.Result().Output);
    }

    // Deltas of the SAME item are never split — a separator is only inserted at an item boundary.
    [Fact]
    public void DeltasWithinOneAgentMessage_AreNotSeparated()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i1","delta":"Hello "}}"""));
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i1","delta":"world"}}"""));

        Assert.Equal("Hello world", reader.Result().Output);
    }

    // A message whose text already ends in a newline must not gain a doubled blank line when the next item starts.
    [Fact]
    public void AgentMessageEndingInNewline_IsNotDoublySeparated()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i1","delta":"first\n"}}"""));
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"i2","delta":"second"}}"""));

        Assert.Equal("first\nsecond", reader.Result().Output);
    }

    // A command the agent runs mid-turn surfaces as a compact, distinctly-kinded tool line — and is display-only,
    // so it is NEVER folded into the reply text (which becomes decisions.md / the handoff).
    [Fact]
    public void CommandExecutionItem_RendersACompactToolLine()
    {
        var reader = new CodexAppServerTurnReader();
        CodexStreamEmission? emission = reader.Apply(Msg(
            """{"method":"item/started","params":{"item":{"id":"c1","type":"commandExecution","command":"git status"}}}"""));

        Assert.NotNull(emission);
        Assert.Equal(AgentStreamChunkKind.ToolCall, emission!.Value.Kind);
        Assert.Equal("$ git status", emission.Value.Text);
        Assert.Equal(string.Empty, reader.Result().Output); // display-only, not part of the reply
    }

    // codex wraps commands in a shell ("bash -lc <script>"); the summary shows the readable inner script.
    [Fact]
    public void CommandExecution_WithShellWrapper_ShowsTheInnerScript()
    {
        var reader = new CodexAppServerTurnReader();
        CodexStreamEmission? emission = reader.Apply(Msg(
            """{"method":"item/started","params":{"item":{"id":"c1","type":"commandExecution","command":["bash","-lc","dotnet build"]}}}"""));

        Assert.Equal("$ dotnet build", emission!.Value.Text);
    }

    // item/started and item/completed carry the same item id; the tool line renders once, not twice.
    [Fact]
    public void CommandExecution_IsRenderedOnceAcrossStartedAndCompleted()
    {
        var reader = new CodexAppServerTurnReader();
        CodexStreamEmission? started = reader.Apply(Msg(
            """{"method":"item/started","params":{"item":{"id":"c1","type":"commandExecution","command":"ls"}}}"""));
        CodexStreamEmission? completed = reader.Apply(Msg(
            """{"method":"item/completed","params":{"item":{"id":"c1","type":"commandExecution","command":"ls","exitCode":0}}}"""));

        Assert.Equal("$ ls", started!.Value.Text);
        Assert.Null(completed); // same item id — not repeated
    }

    // A command line interleaved with the reply must not pollute the accumulated Output.
    [Fact]
    public void ToolCallsAreNotFoldedIntoTheReplyOutput()
    {
        var reader = new CodexAppServerTurnReader();
        reader.Apply(Msg("""{"method":"item/started","params":{"item":{"id":"c1","type":"commandExecution","command":"ls"}}}"""));
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"itemId":"m1","delta":"Done."}}"""));

        Assert.Equal("Done.", reader.Result().Output); // only the agent reply, no "$ ls"
    }

    // Non-tool items (reasoning, etc.) produce no tool line.
    [Fact]
    public void NonToolStartedItems_ProduceNoToolLine()
    {
        var reader = new CodexAppServerTurnReader();
        Assert.Null(reader.Apply(Msg(
            """{"method":"item/started","params":{"item":{"id":"r1","type":"reasoning","text":"thinking"}}}""")));
    }

    // A file edit surfaces as a compact edit line naming the touched paths.
    [Fact]
    public void FileChangeItem_RendersACompactEditLine()
    {
        var reader = new CodexAppServerTurnReader();
        CodexStreamEmission? emission = reader.Apply(Msg(
            """{"method":"item/completed","params":{"item":{"id":"f1","type":"fileChange","changes":[{"path":"a.cs"},{"path":"b.cs"}]}}}"""));

        Assert.Equal(AgentStreamChunkKind.ToolCall, emission!.Value.Kind);
        Assert.Equal("edit a.cs, b.cs", emission.Value.Text);
    }
}

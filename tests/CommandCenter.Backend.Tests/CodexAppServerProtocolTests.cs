using System.Text.Json;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

public sealed class CodexAppServerProtocolTests
{
    private static JsonElement Root(string frame) => JsonDocument.Parse(frame).RootElement;

    [Fact]
    public void InitializeFrameCarriesJsonRpcEnvelopeAndClientInfo()
    {
        JsonElement root = Root(CodexAppServerProtocol.Initialize(1));

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, root.GetProperty("id").GetInt64());
        Assert.Equal("initialize", root.GetProperty("method").GetString());
        Assert.Equal("CommandCenter", root.GetProperty("params").GetProperty("clientInfo").GetProperty("name").GetString());
    }

    [Fact]
    public void ThreadStartFrameMapsCwdSandboxAndApproval()
    {
        JsonElement p = Root(CodexAppServerProtocol.ThreadStart(2, "/repo", "workspace-write", "never"))
            .GetProperty("params");

        Assert.Equal("/repo", p.GetProperty("cwd").GetString());
        Assert.Equal("workspace-write", p.GetProperty("sandbox").GetString());
        Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
    }

    [Fact]
    public void ThreadStartOmitsNullOptionalsSuchAsApprovalPolicy()
    {
        JsonElement p = Root(CodexAppServerProtocol.ThreadStart(2, "/repo", "read-only", approvalPolicy: null))
            .GetProperty("params");

        Assert.False(p.TryGetProperty("approvalPolicy", out _));
        Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
    }

    [Fact]
    public void TurnStartWrapsPromptAsTextUserInputWithEffort()
    {
        JsonElement p = Root(CodexAppServerProtocol.TurnStart(3, "thread-1", "do the thing", "high"))
            .GetProperty("params");

        Assert.Equal("thread-1", p.GetProperty("threadId").GetString());
        Assert.Equal("high", p.GetProperty("effort").GetString());

        JsonElement input = p.GetProperty("input");
        Assert.Equal(1, input.GetArrayLength());
        JsonElement first = input[0];
        Assert.Equal("text", first.GetProperty("type").GetString());
        Assert.Equal("do the thing", first.GetProperty("text").GetString());
        Assert.Equal(0, first.GetProperty("text_elements").GetArrayLength());
    }

    [Fact]
    public void TurnStartOmitsEffortWhenNull()
    {
        JsonElement p = Root(CodexAppServerProtocol.TurnStart(3, "thread-1", "hi", effort: null))
            .GetProperty("params");

        Assert.False(p.TryGetProperty("effort", out _));
    }

    [Fact]
    public void InitializedNotificationHasMethodAndNoId()
    {
        JsonElement root = Root(CodexAppServerProtocol.Initialized());

        Assert.Equal("initialized", root.GetProperty("method").GetString());
        Assert.False(root.TryGetProperty("id", out _));
    }

    [Fact]
    public void ApprovalResponseEchoesRequestIdAndDecision()
    {
        JsonElement root = Root(CodexAppServerProtocol.ApprovalResponse(7L, CodexAppServerProtocol.DeclineDecision));

        Assert.Equal(7, root.GetProperty("id").GetInt64());
        Assert.Equal("decline", root.GetProperty("result").GetProperty("decision").GetString());
    }
}

public sealed class CodexAppServerMessageTests
{
    [Fact]
    public void ResponseWithResultIsClassifiedAsResponse()
    {
        CodexAppServerMessage message = CodexAppServerMessage.Parse(
            """{"jsonrpc":"2.0","id":1,"result":{"thread":{"id":"T1"}}}""");

        Assert.Equal(CodexAppServerMessageKind.Response, message.Kind);
        Assert.Equal(1, message.NumericId);
        Assert.Equal("T1", message.Result.GetProperty("thread").GetProperty("id").GetString());
    }

    [Fact]
    public void ErrorResponseExposesErrorMessage()
    {
        CodexAppServerMessage message = CodexAppServerMessage.Parse(
            """{"jsonrpc":"2.0","id":2,"error":{"message":"boom"}}""");

        Assert.Equal(CodexAppServerMessageKind.Response, message.Kind);
        Assert.Equal("boom", message.ErrorMessage);
    }

    [Fact]
    public void MethodWithoutIdIsANotification()
    {
        CodexAppServerMessage message = CodexAppServerMessage.Parse(
            """{"jsonrpc":"2.0","method":"turn/completed","params":{"turn":{"status":"completed"}}}""");

        Assert.Equal(CodexAppServerMessageKind.Notification, message.Kind);
        Assert.Equal("turn/completed", message.Method);
    }

    [Fact]
    public void MethodWithIdIsAServerRequestThatPreservesAStringId()
    {
        CodexAppServerMessage message = CodexAppServerMessage.Parse(
            """{"jsonrpc":"2.0","id":"req-9","method":"item/commandExecution/requestApproval","params":{}}""");

        Assert.Equal(CodexAppServerMessageKind.ServerRequest, message.Kind);
        Assert.Equal("req-9", message.Id);
        Assert.Null(message.NumericId);
    }

    [Fact]
    public void NonJsonLineIsUnknown()
    {
        Assert.Equal(CodexAppServerMessageKind.Unknown, CodexAppServerMessage.Parse("ready.").Kind);
    }
}

public sealed class CodexAppServerTurnReaderTests
{
    private static CodexAppServerMessage Msg(string json) => CodexAppServerMessage.Parse(json);

    [Fact]
    public void AccumulatesAgentMessageDeltasRealUsageAndCompletion()
    {
        var reader = new CodexAppServerTurnReader();

        reader.Apply(Msg("""{"method":"turn/started","params":{"threadId":"T","turn":{"id":"u1","status":"inProgress"}}}"""));
        string? delta = reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"threadId":"T","turnId":"u1","itemId":"i1","delta":"Hel"}}"""));
        reader.Apply(Msg("""{"method":"item/agentMessage/delta","params":{"threadId":"T","turnId":"u1","itemId":"i1","delta":"lo"}}"""));
        reader.Apply(Msg("""{"method":"thread/tokenUsage/updated","params":{"threadId":"T","turnId":"u1","tokenUsage":{"last":{"totalTokens":37,"inputTokens":30,"cachedInputTokens":1,"outputTokens":7,"reasoningOutputTokens":0}}}}"""));
        Assert.False(reader.IsComplete);
        reader.Apply(Msg("""{"method":"turn/completed","params":{"threadId":"T","turn":{"id":"u1","status":"completed"}}}"""));

        Assert.Equal("Hel", delta); // returned for live streaming
        Assert.True(reader.IsComplete);

        CodexAppServerTurnOutcome outcome = reader.Result();
        Assert.Equal("Hello", outcome.Output); // deltas concatenate into the reply
        Assert.Equal(AgentTurnState.Completed, outcome.State);
        Assert.Equal(30, outcome.Usage!.PromptTokens);
        Assert.Equal(7, outcome.Usage.OutputTokens);
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
}

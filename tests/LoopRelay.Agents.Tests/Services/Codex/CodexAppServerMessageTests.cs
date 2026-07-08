using LoopRelay.Agents.Models.Codex;
using LoopRelay.Agents.Primitives.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

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

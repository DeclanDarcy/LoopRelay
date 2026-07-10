using System.Text.Json;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

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
        Assert.Equal("LoopRelay", root.GetProperty("params").GetProperty("clientInfo").GetProperty("name").GetString());
    }

    [Fact]
    public void ThreadStartFrameMapsCwdSandboxAndApproval()
    {
        JsonElement p = Root(CodexAppServerProtocol.ThreadStart(
                2, "/repo", "workspace-write", "never", "gpt-5.6-sol"))
            .GetProperty("params");

        Assert.Equal("/repo", p.GetProperty("cwd").GetString());
        Assert.Equal("workspace-write", p.GetProperty("sandbox").GetString());
        Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
        Assert.Equal("gpt-5.6-sol", p.GetProperty("model").GetString());
    }

    [Fact]
    public void ThreadStartOmitsNullOptionalsSuchAsApprovalPolicy()
    {
        JsonElement p = Root(CodexAppServerProtocol.ThreadStart(
                2, "/repo", "read-only", approvalPolicy: null, model: "gpt-5.5"))
            .GetProperty("params");

        Assert.False(p.TryGetProperty("approvalPolicy", out _));
        Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
    }

    [Fact]
    public void TurnStartWrapsPromptAsTextUserInputWithEffort()
    {
        JsonElement p = Root(CodexAppServerProtocol.TurnStart(
                3, "thread-1", "do the thing", "gpt-5.6-terra", "high"))
            .GetProperty("params");

        Assert.Equal("thread-1", p.GetProperty("threadId").GetString());
        Assert.Equal("high", p.GetProperty("effort").GetString());
        Assert.Equal("gpt-5.6-terra", p.GetProperty("model").GetString());

        JsonElement input = p.GetProperty("input");
        Assert.Equal(1, input.GetArrayLength());
        JsonElement first = input[0];
        Assert.Equal("text", first.GetProperty("type").GetString());
        Assert.Equal("do the thing", first.GetProperty("text").GetString());
        Assert.Equal(0, first.GetProperty("text_elements").GetArrayLength());
    }

    [Fact]
    public void TurnStartAlwaysCarriesCanonicalModelAndEffort()
    {
        JsonElement p = Root(CodexAppServerProtocol.TurnStart(
                3, "thread-1", "hi", "gpt-5.6-luna", "xhigh"))
            .GetProperty("params");

        Assert.Equal("gpt-5.6-luna", p.GetProperty("model").GetString());
        Assert.Equal("xhigh", p.GetProperty("effort").GetString());
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

    [Fact]
    public void ThreadResumeFrameMapsThreadIdCwdSandboxApprovalAndExcludeTurns()
    {
        JsonElement root = Root(CodexAppServerProtocol.ThreadResume(
            4, "thread-old", "/repo", "read-only", "never", "gpt-5.6-sol"));

        Assert.Equal("thread/resume", root.GetProperty("method").GetString());
        JsonElement p = root.GetProperty("params");
        Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
        Assert.Equal("/repo", p.GetProperty("cwd").GetString());
        Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
        Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
        Assert.Equal("gpt-5.6-sol", p.GetProperty("model").GetString());
        // History replay is never needed (the CLI streams no prior turns) and can be huge — always excluded.
        Assert.True(p.GetProperty("excludeTurns").GetBoolean());
    }

    [Fact]
    public void ThreadResumeOmitsNullOptionals()
    {
        JsonElement p = Root(CodexAppServerProtocol.ThreadResume(
                4, "thread-old", cwd: null, sandbox: null, approvalPolicy: null, model: "gpt-5.5"))
            .GetProperty("params");

        Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
        Assert.False(p.TryGetProperty("cwd", out _));
        Assert.False(p.TryGetProperty("sandbox", out _));
        Assert.False(p.TryGetProperty("approvalPolicy", out _));
    }
}

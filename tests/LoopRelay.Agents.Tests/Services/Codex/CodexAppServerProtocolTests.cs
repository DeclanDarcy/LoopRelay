using System.Text.Json;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexAppServerProtocolTests
{
    private static JsonElement Root(string frame) => JsonDocument.Parse(frame).RootElement;

    [Fact]
    public void InitializeFrameCarriesJsonRpcEnvelopeAndClientInfo()
    {
        JsonElement root = Root(CodexAppServerProtocol.Initialize(1, new CodexInitializeOptions(ExperimentalApi: false)));

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, root.GetProperty("id").GetInt64());
        Assert.Equal("initialize", root.GetProperty("method").GetString());
        Assert.Equal("LoopRelay", root.GetProperty("params").GetProperty("clientInfo").GetProperty("name").GetString());
    }

    [Fact]
    public void InitializeOffersExperimentalApiOnlyWhenAuthorizedByTypedOptions()
    {
        JsonElement enabled = Root(CodexAppServerProtocol.Initialize(1, new CodexInitializeOptions(true)));
        Assert.True(enabled.GetProperty("params").GetProperty("capabilities").GetProperty("experimentalApi").GetBoolean());

        JsonElement disabled = Root(CodexAppServerProtocol.Initialize(1, new CodexInitializeOptions(false)));
        Assert.False(disabled.GetProperty("params").TryGetProperty("capabilities", out _));
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

    [Fact]
    public void ThreadResumeFrameMapsThreadIdCwdSandboxApprovalAndExcludeTurns()
    {
        SessionContinuityProfile profile = Profile(SessionOperationSupport.Supported, SessionOperationSupport.Supported, experimentalApi: true);
        JsonElement root = Root(CodexAppServerProtocol.ThreadResume(
            4, CodexThreadResumeOptions.FromProfile(profile, "thread-old", "/repo", "read-only", "never")));

        Assert.Equal("thread/resume", root.GetProperty("method").GetString());
        JsonElement p = root.GetProperty("params");
        Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
        Assert.Equal("/repo", p.GetProperty("cwd").GetString());
        Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
        Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
        // History replay is never needed (the CLI streams no prior turns) and can be huge — always excluded.
        Assert.True(p.GetProperty("excludeTurns").GetBoolean());
    }

    [Fact]
    public void ThreadResumeOmitsNullOptionals()
    {
        SessionContinuityProfile profile = Profile(SessionOperationSupport.Supported, SessionOperationSupport.Unsupported, experimentalApi: true);
        JsonElement p = Root(CodexAppServerProtocol.ThreadResume(
                4, CodexThreadResumeOptions.FromProfile(profile, "thread-old", cwd: null, sandbox: null, approvalPolicy: null)))
            .GetProperty("params");

        Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
        Assert.False(p.TryGetProperty("cwd", out _));
        Assert.False(p.TryGetProperty("sandbox", out _));
        Assert.False(p.TryGetProperty("approvalPolicy", out _));
        Assert.False(p.TryGetProperty("excludeTurns", out _));
    }

    [Theory]
    [InlineData(SessionOperationSupport.Unknown, SessionOperationSupport.Supported, true)]
    [InlineData(SessionOperationSupport.Unsupported, SessionOperationSupport.Supported, true)]
    [InlineData(SessionOperationSupport.Supported, SessionOperationSupport.Unknown, true)]
    [InlineData(SessionOperationSupport.Supported, SessionOperationSupport.Supported, false)]
    public void ThreadResumeRejectsAnUnauthorizedOperationOrParameter(
        SessionOperationSupport resume,
        SessionOperationSupport excludeTurns,
        bool experimentalApi)
    {
        SessionContinuityProfile profile = Profile(resume, excludeTurns, experimentalApi);
        Assert.Throws<SessionOperationProfileGateException>(() =>
            CodexThreadResumeOptions.FromProfile(profile, "thread-old", null, null, null));
    }

    [Fact]
    public void ThreadReadIsEmittedOnlyForSupportedProfile()
    {
        SessionContinuityProfile supported = Profile(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, experimentalApi: true,
            read: SessionOperationSupport.Supported);
        JsonElement root = Root(CodexAppServerProtocol.ThreadRead(
            9, CodexThreadReadOptions.FromProfile(supported, "thread-1")));
        Assert.Equal("thread/read", root.GetProperty("method").GetString());
        Assert.True(root.GetProperty("params").GetProperty("includeTurns").GetBoolean());

        SessionContinuityProfile unknown = Profile(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, experimentalApi: true,
            read: SessionOperationSupport.Unknown);
        Assert.Throws<SessionOperationProfileGateException>(() =>
            CodexThreadReadOptions.FromProfile(unknown, "thread-1"));
    }

    [Fact]
    public void ThreadForkIsEmittedOnlyForSupportedProfile()
    {
        SessionContinuityProfile supported = Profile(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, experimentalApi: true,
            fork: SessionOperationSupport.Supported);
        JsonElement root = Root(CodexAppServerProtocol.ThreadFork(
            10, CodexThreadForkOptions.FromProfile(
                supported, "thread-parent", "/repo", "read-only", "never")));
        Assert.Equal("thread/fork", root.GetProperty("method").GetString());
        Assert.Equal("thread-parent", root.GetProperty("params").GetProperty("threadId").GetString());

        SessionContinuityProfile unknown = Profile(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, experimentalApi: true,
            fork: SessionOperationSupport.Unknown);
        Assert.Throws<SessionOperationProfileGateException>(() =>
            CodexThreadForkOptions.FromProfile(unknown, "thread-parent", null, null, null));
    }

    private static SessionContinuityProfile Profile(
        SessionOperationSupport resume,
        SessionOperationSupport excludeTurns,
        bool experimentalApi,
        SessionOperationSupport read = SessionOperationSupport.Unknown,
        SessionOperationSupport fork = SessionOperationSupport.Unknown)
    {
        var descriptor = new SessionOperationSupportDescriptor(
            resume,
            "v2",
            new Dictionary<string, SessionParameterSupport>
            {
                [SessionContinuityProfile.ExcludeTurnsParameter] = new(excludeTurns, "test"),
            },
            "load",
            "same-id",
            "none",
            "read",
            "test");
        var operations = new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
        {
            [SessionContinuityOperation.Resume] = descriptor,
            [SessionContinuityOperation.ConversationRead] = new SessionOperationSupportDescriptor(
                read, "thread/read", new Dictionary<string, SessionParameterSupport>(),
                "read", "records", "none", "repeat", "test"),
            [SessionContinuityOperation.Fork] = new SessionOperationSupportDescriptor(
                fork, "thread/fork", new Dictionary<string, SessionParameterSupport>(),
                "clone", "stable-parent-child", "none", "enumerate", "test"),
        };
        return new SessionContinuityProfile(
            "codex", "test", "test", null, "v2", "digest",
            new Dictionary<string, bool> { ["experimentalApi"] = experimentalApi },
            new Dictionary<string, string>(),
            operations,
            null, "unknown", "test", negotiatedAt: DateTimeOffset.UnixEpoch);
    }
}

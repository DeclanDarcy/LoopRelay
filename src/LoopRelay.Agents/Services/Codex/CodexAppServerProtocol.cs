using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Services.Codex;

/// <summary>
/// Builds outbound Codex app-server JSON-RPC 2.0 request frames (codex-cli 0.139, v2 protocol):
/// the <c>initialize</c> handshake, <c>thread/start</c>, <c>thread/resume</c>, <c>turn/start</c>, and replies to the
/// server's mid-turn approval requests. Pure serialization — the session layer maps an
/// <c>AgentSessionSpec</c> to the primitive arguments here and owns the transport/correlation.
/// </summary>
public static class CodexAppServerProtocol
{
    public const string ClientName = "LoopRelay";
    public const string ClientVersion = "0.1.0";
    public const string DeclineDecision = "decline";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>The required first request; negotiates capabilities and identifies the client.</summary>
    public static string Initialize(long id, CodexInitializeOptions options)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["clientInfo"] = new Dictionary<string, object?>
            {
                ["name"] = ClientName,
                ["version"] = ClientVersion
            }
        };
        if (options.ExperimentalApi)
        {
            parameters["capabilities"] = new Dictionary<string, object?> { ["experimentalApi"] = true };
        }

        return Request(id, "initialize", parameters);
    }

    /// <summary>
    /// The notification the client sends immediately after the <c>initialize</c> response and before
    /// any thread/turn request (LSP-style handshake completion; confirmed against the reference client).
    /// </summary>
    public static string Initialized() =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "initialized"
        }, Options);

    /// <summary>Creates a thread; the response carries <c>thread.id</c> used to address turns.</summary>
    public static string ThreadStart(long id, string? cwd, string? sandbox, string? approvalPolicy) =>
        Request(id, "thread/start", Compact(new Dictionary<string, object?>
        {
            ["cwd"] = cwd,
            ["sandbox"] = sandbox,
            ["approvalPolicy"] = approvalPolicy
        }));

    /// <summary>
    /// Resumes a previously persisted thread by id (codex loads it from its own rollout on disk). The response
    /// carries the same <c>thread.id</c> shape as <c>thread/start</c>. <c>excludeTurns</c> is always true —
    /// replayed history is never consumed and can be arbitrarily large. Verified against codex-cli 0.142.5.
    /// </summary>
    public static string ThreadResume(long id, CodexThreadResumeOptions options)
    {
        var parameters = Compact(new Dictionary<string, object?>
        {
            ["threadId"] = options.ThreadId,
            ["cwd"] = options.Cwd,
            ["sandbox"] = options.Sandbox,
            ["approvalPolicy"] = options.ApprovalPolicy,
        });
        if (options.ExcludeTurns)
        {
            parameters["excludeTurns"] = true;
        }

        return Request(id, "thread/resume", parameters);
    }

    public static string ThreadRead(long id, CodexThreadReadOptions options) =>
        Request(id, "thread/read", new Dictionary<string, object?>
        {
            ["threadId"] = options.ThreadId,
            ["includeTurns"] = options.IncludeTurns,
        });

    public static string ThreadFork(long id, CodexThreadForkOptions options) =>
        Request(id, "thread/fork", Compact(new Dictionary<string, object?>
        {
            ["threadId"] = options.ParentThreadId,
            ["cwd"] = options.Cwd,
            ["sandbox"] = options.Sandbox,
            ["approvalPolicy"] = options.ApprovalPolicy,
        }));

    /// <summary>Submits a turn to an existing thread. The prompt is the turn's text user input.</summary>
    public static string TurnStart(long id, string threadId, string prompt, string? effort) =>
        Request(id, "turn/start", Compact(new Dictionary<string, object?>
        {
            ["threadId"] = threadId,
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["text"] = prompt,
                    ["text_elements"] = Array.Empty<object>()
                }
            },
            ["effort"] = effort
        }));

    /// <summary>Replies to a server-to-client approval request. <paramref name="requestId"/> must echo the request's id.</summary>
    public static string ApprovalResponse(object requestId, string decision) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["result"] = new Dictionary<string, object?> { ["decision"] = decision }
        }, Options);

    // System.Text.Json's WhenWritingNull ignores null object PROPERTIES, not null dictionary
    // VALUES, so optional params are stripped explicitly before serialization.
    private static Dictionary<string, object?> Compact(Dictionary<string, object?> map)
    {
        foreach (string key in map.Where(entry => entry.Value is null).Select(entry => entry.Key).ToList())
        {
            map.Remove(key);
        }

        return map;
    }

    private static string Request(long id, string method, object @params) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params
        }, Options);
}

public sealed record CodexInitializeOptions(bool ExperimentalApi)
{
    public static CodexInitializeOptions FromProfile(SessionContinuityProfile profile) =>
        new(profile.OfferedClientCapabilities.TryGetValue("experimentalApi", out bool offered) && offered);
}

public sealed record CodexThreadResumeOptions(
    string ThreadId,
    string? Cwd,
    string? Sandbox,
    string? ApprovalPolicy,
    bool ExcludeTurns)
{
    public static CodexThreadResumeOptions FromProfile(
        SessionContinuityProfile profile,
        string threadId,
        string? cwd,
        string? sandbox,
        string? approvalPolicy)
    {
        if (profile.Operation(SessionContinuityOperation.Resume).Status != SessionOperationSupport.Supported)
        {
            throw new SessionOperationProfileGateException("thread/resume is not Supported by the captured continuity profile.");
        }

        SessionParameterSupport excludeTurns = profile.Parameter(
            SessionContinuityOperation.Resume,
            SessionContinuityProfile.ExcludeTurnsParameter);
        if (excludeTurns.Status == SessionOperationSupport.Unknown)
        {
            throw new SessionOperationProfileGateException("thread/resume excludeTurns support is Unknown; the request was not emitted.");
        }

        bool experimentalOffered = profile.OfferedClientCapabilities.TryGetValue("experimentalApi", out bool offered) && offered;
        if (excludeTurns.Status == SessionOperationSupport.Supported && !experimentalOffered)
        {
            throw new SessionOperationProfileGateException(
                "thread/resume excludeTurns requires capabilities.experimentalApi=true in initialize.");
        }

        return new CodexThreadResumeOptions(
            threadId,
            cwd,
            sandbox,
            approvalPolicy,
            ExcludeTurns: excludeTurns.Status == SessionOperationSupport.Supported);
    }
}

public sealed class SessionOperationProfileGateException(string message) : InvalidOperationException(message);

public sealed record CodexThreadReadOptions(string ThreadId, bool IncludeTurns)
{
    public static CodexThreadReadOptions FromProfile(SessionContinuityProfile profile, string threadId)
    {
        if (profile.Operation(SessionContinuityOperation.ConversationRead).Status != SessionOperationSupport.Supported)
        {
            throw new SessionOperationProfileGateException(
                "thread/read is not Supported by the captured continuity profile.");
        }

        return new CodexThreadReadOptions(threadId, IncludeTurns: true);
    }
}

public sealed record CodexThreadForkOptions(
    string ParentThreadId,
    string? Cwd,
    string? Sandbox,
    string? ApprovalPolicy)
{
    public static CodexThreadForkOptions FromProfile(
        SessionContinuityProfile profile,
        string parentThreadId,
        string? cwd,
        string? sandbox,
        string? approvalPolicy)
    {
        if (profile.Operation(SessionContinuityOperation.Fork).Status != SessionOperationSupport.Supported)
        {
            throw new SessionOperationProfileGateException(
                "thread/fork is not Supported by the captured continuity profile.");
        }

        return new CodexThreadForkOptions(parentThreadId, cwd, sandbox, approvalPolicy);
    }
}

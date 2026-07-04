using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Builds outbound Codex app-server JSON-RPC 2.0 request frames (codex-cli 0.139, v2 protocol):
/// the <c>initialize</c> handshake, <c>thread/start</c>, <c>thread/resume</c>, <c>turn/start</c>, and replies to the
/// server's mid-turn approval requests. Pure serialization — the session layer maps an
/// <c>AgentSessionSpec</c> to the primitive arguments here and owns the transport/correlation.
/// </summary>
public static class CodexAppServerProtocol
{
    public const string ClientName = "CommandCenter";
    public const string ClientVersion = "0.1.0";
    public const string DeclineDecision = "decline";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>The required first request; negotiates capabilities and identifies the client.</summary>
    public static string Initialize(long id) =>
        Request(id, "initialize", new Dictionary<string, object?>
        {
            ["clientInfo"] = new Dictionary<string, object?>
            {
                ["name"] = ClientName,
                ["version"] = ClientVersion
            }
        });

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
    public static string ThreadResume(long id, string threadId, string? cwd, string? sandbox, string? approvalPolicy) =>
        Request(id, "thread/resume", Compact(new Dictionary<string, object?>
        {
            ["threadId"] = threadId,
            ["cwd"] = cwd,
            ["sandbox"] = sandbox,
            ["approvalPolicy"] = approvalPolicy,
            ["excludeTurns"] = true
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

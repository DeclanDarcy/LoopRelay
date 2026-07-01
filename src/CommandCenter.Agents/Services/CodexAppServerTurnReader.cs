using System.Text;
using System.Text.Json;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>The accumulated result of one Codex app-server turn.</summary>
public sealed record CodexAppServerTurnOutcome(
    string Output,
    AgentTokenUsage? Usage,
    AgentTurnState State,
    string? FailureMessage);

/// <summary>
/// Accumulates a single Codex app-server turn from its notification stream: the agent reply text
/// from <c>item/agentMessage/delta</c> chunks (the reply streams as deltas — confirmed against the
/// reference Conduit.Codex client and the live protocol; a completed agent-message item is only a
/// fallback when no deltas arrived), the real token usage from <c>thread/tokenUsage/updated</c>, and
/// the terminal state from <c>turn/completed</c>. Deltas are also returned for live surfacing.
/// One reader instance accumulates one turn.
/// </summary>
public sealed class CodexAppServerTurnReader
{
    private readonly StringBuilder output = new();
    private AgentTokenUsage? usage;
    private AgentTurnState? terminalState;
    private string? failureMessage;

    public bool IsComplete => terminalState is not null;

    /// <summary>
    /// Applies one inbound message. Returns the agent-message delta text when the message is a
    /// streaming delta (for live surfacing), otherwise null.
    /// </summary>
    public string? Apply(CodexAppServerMessage message)
    {
        if (message.Kind != CodexAppServerMessageKind.Notification || message.Method is null)
        {
            return null;
        }

        switch (message.Method)
        {
            case "item/agentMessage/delta":
                string? delta = StringProperty(message.Params, "delta");
                if (!string.IsNullOrEmpty(delta))
                {
                    output.Append(delta);
                }

                return delta;

            case "item/completed":
                // The reply streams as deltas; a completed item is only a fallback if none arrived.
                if (output.Length == 0)
                {
                    AppendAgentMessage(message.Params);
                }

                return null;

            case "thread/tokenUsage/updated":
                usage = ReadUsage(message.Params) ?? usage;
                return null;

            case "turn/completed":
                Complete(message.Params);
                return null;

            case "error":
                failureMessage ??= ReadErrorMessage(message.Params) ?? message.ErrorMessage;
                return null;

            default:
                return null;
        }
    }

    public CodexAppServerTurnOutcome Result() =>
        new(output.ToString(), usage, terminalState ?? AgentTurnState.Failed, failureMessage);

    private void AppendAgentMessage(JsonElement @params)
    {
        if (!@params.TryGetProperty("item", out JsonElement item) || item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string type = (StringProperty(item, "type") ?? string.Empty).ToLowerInvariant();
        if (!type.Contains("agent") && !type.Contains("assistant"))
        {
            return;
        }

        if (ItemText(item) is not { Length: > 0 } text)
        {
            return;
        }

        if (output.Length > 0)
        {
            output.Append('\n');
        }

        output.Append(text);
    }

    private void Complete(JsonElement @params)
    {
        string status = @params.TryGetProperty("turn", out JsonElement turn) && turn.ValueKind == JsonValueKind.Object
            ? (StringProperty(turn, "status") ?? string.Empty)
            : string.Empty;

        terminalState = status switch
        {
            "completed" => AgentTurnState.Completed,
            "interrupted" => AgentTurnState.Canceled,
            _ => AgentTurnState.Failed
        };

        if (terminalState == AgentTurnState.Failed
            && turn.ValueKind == JsonValueKind.Object
            && turn.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object)
        {
            failureMessage ??= StringProperty(error, "message");
        }
    }

    private static AgentTokenUsage? ReadUsage(JsonElement @params)
    {
        if (!@params.TryGetProperty("tokenUsage", out JsonElement tokenUsage)
            || !tokenUsage.TryGetProperty("last", out JsonElement last)
            || last.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int input = IntProperty(last, "inputTokens") ?? 0;
        int outputTokens = IntProperty(last, "outputTokens") ?? 0;
        int cachedInput = IntProperty(last, "cachedInputTokens") ?? 0;
        return new AgentTokenUsage(input, outputTokens, cachedInput);
    }

    private static string? ReadErrorMessage(JsonElement @params) =>
        @params.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object
            ? StringProperty(error, "message")
            : null;

    private static string? ItemText(JsonElement item)
    {
        if (StringProperty(item, "text") is { } text)
        {
            return text;
        }

        if (item.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (JsonElement part in content.EnumerateArray())
            {
                if (StringProperty(part, "text") is { } partText)
                {
                    parts.Add(partText);
                }
            }

            if (parts.Count > 0)
            {
                return string.Concat(parts);
            }
        }

        return null;
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? IntProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int parsed)
            ? parsed
            : null;
}

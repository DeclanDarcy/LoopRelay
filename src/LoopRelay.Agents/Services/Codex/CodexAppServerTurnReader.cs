using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Models.Codex;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Codex;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Primitives.Streams;

namespace LoopRelay.Agents.Services.Codex;

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
    private string? currentItemId;   // the agent-message item the deltas currently belong to
    private readonly HashSet<string> renderedToolItems = new();   // tool items already surfaced (dedupe started/completed)

    public bool IsComplete => terminalState is not null;

    /// <summary>
    /// Applies one inbound message. Returns an emission — an agent-reply delta or a compact tool-call summary —
    /// when the message should be surfaced live, otherwise null. Tool-call summaries are display-only and are
    /// NEVER folded into the accumulated reply (<see cref="CodexAppServerTurnOutcome.Output"/>).
    /// </summary>
    public CodexStreamEmission? Apply(CodexAppServerMessage message)
    {
        if (message.Kind != CodexAppServerMessageKind.Notification || message.Method is null)
        {
            return null;
        }

        switch (message.Method)
        {
            case "item/agentMessage/delta":
                return ApplyAgentMessageDelta(message.Params) is { } delta
                    ? new CodexStreamEmission(delta, AgentStreamChunkKind.AgentMessage)
                    : null;

            case "item/started":
                // Tool calls (command executions, file edits, MCP/web calls) surface as a compact one-liner the
                // moment they start; deduped by item id so the matching item/completed doesn't repeat them.
                return RenderToolItem(message.Params);

            case "item/completed":
                if (RenderToolItem(message.Params) is { } tool)
                {
                    return tool;
                }

                // The reply streams as deltas; a completed agent item is only a fallback if none arrived.
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

    // A turn's reply can arrive as SEVERAL agent-message items (codex narrates a long turn as separate messages),
    // and their deltas would otherwise concatenate into one run-on blob — most visible on execution turns. When a
    // new item's first delta arrives after existing output, insert a newline so each message lands on its own line.
    // The separator is prepended to the returned delta too, so the live console stream breaks in the same place.
    private string? ApplyAgentMessageDelta(JsonElement @params)
    {
        string? delta = StringProperty(@params, "delta");
        if (string.IsNullOrEmpty(delta))
        {
            return null;
        }

        string? itemId = StringProperty(@params, "itemId");
        string separator = string.Empty;
        if (itemId is not null && currentItemId is not null && itemId != currentItemId
            && output.Length > 0 && output[^1] != '\n')
        {
            separator = "\n";
            output.Append('\n');
        }

        currentItemId = itemId ?? currentItemId;
        output.Append(delta);
        return separator + delta;
    }

    // A tool call the agent ran mid-turn — a shell command, a file edit, an MCP or web call — surfaced as ONE
    // compact line (display-only, never part of the reply). Returns null for non-tool items (agent message,
    // reasoning, todo list, unknown) and for an item already rendered (item/started then item/completed share id).
    private CodexStreamEmission? RenderToolItem(JsonElement @params)
    {
        if (!@params.TryGetProperty("item", out JsonElement item) || item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (CodexToolCallFormatter.Format(item) is not { Length: > 0 } summary)
        {
            return null;
        }

        // Dedupe by item id across started/completed. Items without an id (defensive) still render once each.
        string? itemId = StringProperty(item, "id") ?? StringProperty(@params, "itemId");
        if (itemId is not null && !renderedToolItems.Add(itemId))
        {
            return null;
        }

        return new CodexStreamEmission(summary, AgentStreamChunkKind.ToolCall);
    }

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

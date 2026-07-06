using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Services;

/// <summary>The accumulated result of one Codex app-server turn.</summary>
public sealed record CodexAppServerTurnOutcome(
    string Output,
    AgentTokenUsage? Usage,
    AgentTurnState State,
    string? FailureMessage);

/// <summary>One live surfacing from a turn: either an agent-reply delta or a compact tool-call summary.</summary>
public readonly record struct CodexStreamEmission(string Text, AgentStreamChunkKind Kind);

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

        string type = (StringProperty(item, "type") ?? string.Empty).ToLowerInvariant();
        if (ToolSummary(type, item) is not { Length: > 0 } summary)
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

    // The compact summary for a tool item, or null when the item is not a tool call. Command execution is the
    // common case; file edits, MCP tool calls, and web searches get a minimal label. Anything else -> null.
    private static string? ToolSummary(string type, JsonElement item)
    {
        if (type.Contains("command"))   // codex item type "commandExecution"
        {
            return CommandText(item) is { Length: > 0 } command ? $"$ {Compact(command)}" : null;
        }

        if (type.Contains("filechange") || type.Contains("file_change") || type.Contains("patch"))
        {
            return FileChangeText(item) is { Length: > 0 } files ? $"edit {Compact(files)}" : "edit (files)";
        }

        if (type.Contains("mcp"))       // "mcpToolCall"
        {
            string? server = StringProperty(item, "server");
            string? tool = StringProperty(item, "tool") ?? StringProperty(item, "name");
            string label = server is not null && tool is not null ? $"{server}/{tool}" : tool ?? server ?? "call";
            return $"tool {Compact(label)}";
        }

        if (type.Contains("websearch") || type.Contains("web_search"))
        {
            return (StringProperty(item, "query") ?? StringProperty(item, "text")) is { Length: > 0 } query
                ? $"web {Compact(query)}"
                : "web search";
        }

        return null; // agentMessage, reasoning, todoList, unknown — not a tool line
    }

    // codex command items carry either a "command" string or an argv array (often a shell wrapper like
    // ["bash","-lc","git status"]). Prefer the readable inner script; else join the argv.
    private static string? CommandText(JsonElement item)
    {
        if (StringProperty(item, "command") is { Length: > 0 } text)
        {
            return text;
        }

        if (!item.TryGetProperty("command", out JsonElement argv) || argv.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (JsonElement element in argv.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                parts.Add(element.GetString()!);
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        // Unwrap "<shell> -lc/-c <script>" to just the script — the noise the wrapper adds isn't worth showing.
        if (parts.Count >= 3 && (parts[1] == "-lc" || parts[1] == "-c"))
        {
            return parts[^1];
        }

        return string.Join(' ', parts);
    }

    // A file-change item lists the touched paths (shape varies: an array of {path,...} or a map keyed by path).
    private static string? FileChangeText(JsonElement item)
    {
        if (!item.TryGetProperty("changes", out JsonElement changes))
        {
            return null;
        }

        var paths = new List<string>();
        if (changes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement change in changes.EnumerateArray())
            {
                if (StringProperty(change, "path") is { } path)
                {
                    paths.Add(path);
                }
            }
        }
        else if (changes.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in changes.EnumerateObject())
            {
                paths.Add(property.Name);
            }
        }

        if (paths.Count == 0)
        {
            return null;
        }

        return paths.Count <= 3
            ? string.Join(", ", paths)
            : $"{string.Join(", ", paths.GetRange(0, 3))} +{paths.Count - 3} more";
    }

    // Collapse to a single line and cap the length so one tool call is always one short console line.
    private static string Compact(string value)
    {
        int newline = value.AsSpan().IndexOfAny('\n', '\r');
        string line = (newline >= 0 ? value[..newline] : value).Trim();
        const int max = 160;
        return line.Length <= max ? line : line[..(max - 3)] + "...";
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

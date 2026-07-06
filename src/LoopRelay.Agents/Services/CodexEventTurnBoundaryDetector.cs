using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Services;

/// <summary>
/// Parses Codex <c>exec --json</c> JSONL events (codex-cli 0.139) into per-line turn inspections,
/// replacing the synthetic <see cref="SentinelTurnBoundaryDetector"/> now that <c>codex proto</c>
/// no longer exists. A <c>turn.completed</c> event ends the turn and carries Codex-reported token
/// usage (retiring the character-count estimate); agent-message events surface their text as the
/// turn's output; every other event (reasoning, command execution, thread lifecycle) is ignored so
/// it never pollutes the turn output.
/// </summary>
/// <remarks>
/// The protocol appears both as '/'-separated app-server method names (<c>turn/completed</c>) and
/// '.'-separated exec event types (<c>turn.completed</c>); both are normalised. Event and field
/// names were taken from the installed Codex app-server schema (<c>codex app-server generate-ts</c>);
/// the exact <c>exec --json</c> line shape should be confirmed against a live authenticated run —
/// the recognised names are centralised here so a correction is a one-line change.
/// </remarks>
public sealed class CodexEventTurnBoundaryDetector : IAgentTurnBoundaryDetector
{
    public AgentLineInspection Inspect(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new AgentLineInspection(AgentLineClassification.Ignored);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                // Not a protocol object: surface verbatim rather than silently drop it.
                return new AgentLineInspection(AgentLineClassification.Output, Content: line);
            }

            string kind = NormalizeKind(EventKind(root));

            if (kind is "turn.completed" or "turn.failed")
            {
                return new AgentLineInspection(AgentLineClassification.TurnCompleted, ParseUsage(root));
            }

            string? message = AgentMessageText(root, kind);
            return message is not null
                ? new AgentLineInspection(AgentLineClassification.Output, Content: message)
                : new AgentLineInspection(AgentLineClassification.Ignored);
        }
        catch (JsonException)
        {
            return new AgentLineInspection(AgentLineClassification.Output, Content: line);
        }
    }

    private static string EventKind(JsonElement root) =>
        StringProperty(root, "type") ?? StringProperty(root, "method") ?? string.Empty;

    private static string NormalizeKind(string kind) => kind.Replace('/', '.').ToLowerInvariant();

    private static string? AgentMessageText(JsonElement root, string kind)
    {
        if (kind.Contains("delta") && kind.Contains("message"))
        {
            return StringProperty(root, "delta")
                ?? StringProperty(root, "text")
                ?? StringProperty(Params(root), "delta")
                ?? StringProperty(Params(root), "text");
        }

        if (kind is "item.completed")
        {
            JsonElement item = ItemElement(root);
            if (item.ValueKind == JsonValueKind.Object && IsAgentMessageItem(item))
            {
                return ItemText(item);
            }
        }

        return null;
    }

    private static bool IsAgentMessageItem(JsonElement item)
    {
        string type = (StringProperty(item, "type") ?? string.Empty).ToLowerInvariant();
        return type.Contains("agent") || type.Contains("assistant");
    }

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

    private static AgentTokenUsage? ParseUsage(JsonElement root)
    {
        foreach (JsonElement scope in UsageScopes(root))
        {
            int? input = IntProperty(scope, "input_tokens", "inputTokens", "prompt_tokens", "promptTokens");
            int? output = IntProperty(scope, "output_tokens", "outputTokens", "completion_tokens", "completionTokens");
            int? cachedInput = IntProperty(scope, "cached_input_tokens", "cachedInputTokens");
            if (input is not null || output is not null)
            {
                return new AgentTokenUsage(input ?? 0, output ?? 0, cachedInput ?? 0);
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> UsageScopes(JsonElement root)
    {
        foreach (JsonElement container in new[] { root, Params(root) })
        {
            if (container.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            yield return container;
            foreach (string name in new[] { "usage", "token_usage", "tokenUsage", "tokens" })
            {
                if (container.TryGetProperty(name, out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
                {
                    yield return nested;
                }
            }
        }
    }

    private static JsonElement Params(JsonElement root) =>
        root.TryGetProperty("params", out JsonElement value) ? value : default;

    private static JsonElement ItemElement(JsonElement root)
    {
        if (root.TryGetProperty("item", out JsonElement item))
        {
            return item;
        }

        JsonElement parameters = Params(root);
        return parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("item", out JsonElement nested)
            ? nested
            : default;
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? IntProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out int parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}

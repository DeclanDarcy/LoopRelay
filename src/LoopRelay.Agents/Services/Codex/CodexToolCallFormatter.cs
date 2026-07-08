using System.Text.Json;

namespace LoopRelay.Agents.Services.Codex;

internal static class CodexToolCallFormatter
{
    public static string? Format(JsonElement item)
    {
        string type = (StringProperty(item, "type") ?? string.Empty).ToLowerInvariant();

        if (type.Contains("command"))
        {
            return CommandText(item) is { Length: > 0 } command ? $"$ {Compact(command)}" : null;
        }

        if (type.Contains("filechange") || type.Contains("file_change") || type.Contains("patch"))
        {
            return FileChangeText(item) is { Length: > 0 } files ? $"edit {Compact(files)}" : "edit (files)";
        }

        if (type.Contains("mcp"))
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

        return null;
    }

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

        if (parts.Count >= 3 && (parts[1] == "-lc" || parts[1] == "-c"))
        {
            return parts[^1];
        }

        return string.Join(' ', parts);
    }

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

    private static string Compact(string value)
    {
        int newline = value.AsSpan().IndexOfAny('\n', '\r');
        string line = (newline >= 0 ? value[..newline] : value).Trim();
        const int max = 160;
        return line.Length <= max ? line : line[..(max - 3)] + "...";
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

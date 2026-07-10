using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Models.Sessions;

namespace LoopRelay.Agents.Services.Codex;

public enum CodexThreadReadStatus
{
    Complete,
    Partial,
    Corrupt,
    IdentityMismatch,
}

public sealed record CodexThreadReadResult(
    CodexThreadReadStatus Status,
    string? ThreadId,
    IReadOnlyList<SessionContentRecord> Records,
    string? Digest,
    string? VerifiedBoundary,
    IReadOnlyList<string> Omissions,
    string? Diagnostic);

/// <summary>Parses only the public projection returned by thread/read. Unknown and reasoning items are omitted.</summary>
public sealed class CodexThreadReadParser
{
    public const string Version = "codex-thread-read-public.v1";

    public CodexThreadReadResult Parse(JsonElement result, string expectedThreadId)
    {
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("thread", out JsonElement thread)
            || thread.ValueKind != JsonValueKind.Object)
        {
            return Corrupt("thread/read result did not contain an object thread projection.");
        }

        string? threadId = String(thread, "id");
        if (!string.Equals(threadId, expectedThreadId, StringComparison.Ordinal))
        {
            return new CodexThreadReadResult(
                CodexThreadReadStatus.IdentityMismatch, threadId, [], null, null, [],
                "thread/read returned a different provider thread id.");
        }

        if (!thread.TryGetProperty("turns", out JsonElement turns) || turns.ValueKind != JsonValueKind.Array)
        {
            return Corrupt("thread/read result did not contain a turns array.") with { ThreadId = threadId };
        }

        var records = new List<SessionContentRecord>();
        var omissions = new SortedSet<string>(StringComparer.Ordinal);
        int turnIndex = 0;
        foreach (JsonElement turn in turns.EnumerateArray())
        {
            if (turn.ValueKind != JsonValueKind.Object)
            {
                omissions.Add($"unsupported-turn:{turnIndex}");
                turnIndex++;
                continue;
            }

            if (!turn.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                omissions.Add($"missing-items:{turnIndex}");
                turnIndex++;
                continue;
            }

            int itemIndex = 0;
            foreach (JsonElement item in items.EnumerateArray())
            {
                string? type = String(item, "type");
                if (type is "reasoning" or "analysis" || item.GetRawText().Contains("encrypted_content", StringComparison.Ordinal))
                {
                    omissions.Add($"hidden-reasoning:{turnIndex}:{itemIndex}");
                }
                else if (ReadPublicRecord(item, records.Count, turnIndex, itemIndex) is { } record)
                {
                    records.Add(record);
                }
                else
                {
                    omissions.Add($"unsupported-item:{turnIndex}:{itemIndex}:{type ?? "unknown"}");
                }

                itemIndex++;
            }

            string? turnStatus = String(turn, "status");
            if (turnStatus is "failed" or "interrupted")
            {
                records.Add(new SessionContentRecord(
                    records.Count, "turn-boundary", "provider",
                    $"turn-status:{turnStatus}", String(turn, "id"),
                    new Dictionary<string, string> { ["turn"] = turnIndex.ToString() }));
            }

            turnIndex++;
        }

        string canonical = JsonSerializer.Serialize(result);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new CodexThreadReadResult(
            omissions.Count == 0 ? CodexThreadReadStatus.Complete : CodexThreadReadStatus.Partial,
            threadId,
            records,
            digest,
            $"turn:{turnIndex}",
            omissions.ToArray(),
            null);
    }

    private static SessionContentRecord? ReadPublicRecord(JsonElement item, int order, int turn, int index)
    {
        string? type = String(item, "type");
        string? role = String(item, "role");
        string? text = String(item, "text") ?? String(item, "message") ?? ReadContent(item);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (type == "contextCompaction")
            {
                text = "context-compaction";
                role = "provider";
            }
            else if (type is "commandExecution" or "fileChange" or "mcpToolCall"
                     or "dynamicToolCall" or "webSearch" or "collabAgentToolCall")
            {
                string status = String(item, "status") ?? "unknown";
                text = $"{type}:status={status}";
                role = "tool-summary";
            }
            else
            {
                return null;
            }
        }

        if (type is "userMessage" or "user_message" or "input_text")
        {
            role = "user";
        }
        else if (type is "agentMessage" or "agent_message" or "output_text")
        {
            role = "assistant";
        }

        if (role is not ("user" or "assistant" or "provider" or "tool-summary"))
        {
            return null;
        }

        return new SessionContentRecord(
            order,
            type ?? "message",
            role,
            text,
            String(item, "id"),
            new Dictionary<string, string>
            {
                ["turn"] = turn.ToString(),
                ["item"] = index.ToString(),
            });
    }

    private static string? ReadContent(JsonElement item)
    {
        if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string text = string.Concat(content.EnumerateArray()
            .Where(part => String(part, "type") is "input_text" or "output_text" or "text")
            .Select(part => String(part, "text"))
            .Where(value => value is not null));
        return text.Length == 0 ? null : text;
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static CodexThreadReadResult Corrupt(string diagnostic) =>
        new(CodexThreadReadStatus.Corrupt, null, [], null, null, [], diagnostic);
}

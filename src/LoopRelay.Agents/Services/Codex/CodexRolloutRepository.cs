using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Models.Sessions;

namespace LoopRelay.Agents.Services.Codex;

public enum CodexRolloutReadStatus
{
    Complete,
    Partial,
    Absent,
    Corrupt,
    PermissionDenied,
    Ambiguous,
}

public sealed record CodexRolloutReadResult(
    CodexRolloutReadStatus Status,
    string ThreadId,
    string? Location,
    IReadOnlyList<SessionContentRecord> Records,
    string? Digest,
    string? VerifiedBoundary,
    IReadOnlyList<string> Omissions,
    string? Diagnostic);

public sealed class CodexRolloutRepository
{
    public async Task<CodexRolloutReadResult> ReadExactAsync(
        string codexHome,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(codexHome);
        var matches = new List<CodexRolloutReadResult>();
        try
        {
            foreach (string directoryName in new[] { "sessions", "archived_sessions", "archived" })
            {
                string directory = Path.Combine(root, directoryName);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CodexRolloutReadResult parsed = await ParseAsync(path, threadId, cancellationToken);
                    if (parsed.Status != CodexRolloutReadStatus.Absent)
                    {
                        matches.Add(parsed);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            return new CodexRolloutReadResult(
                CodexRolloutReadStatus.PermissionDenied, threadId, null, [], null, null, [], exception.GetType().Name);
        }

        if (matches.Count == 0)
        {
            return new CodexRolloutReadResult(
                CodexRolloutReadStatus.Absent, threadId, null, [], null, null, [], "Exact provider thread id was not found.");
        }

        if (matches.Count > 1)
        {
            return new CodexRolloutReadResult(
                CodexRolloutReadStatus.Ambiguous, threadId, null, [], null, null, [],
                $"Exact provider thread id resolved to {matches.Count} rollout files.");
        }

        return matches[0];
    }

    private static async Task<CodexRolloutReadResult> ParseAsync(
        string path,
        string expectedThreadId,
        CancellationToken cancellationToken)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new CodexRolloutReadResult(
                CodexRolloutReadStatus.PermissionDenied, expectedThreadId, path, [], null, null, [], exception.GetType().Name);
        }

        string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var records = new List<SessionContentRecord>();
        var omissions = new HashSet<string>(StringComparer.Ordinal);
        string? discoveredId = null;
        int verifiedLine = 0;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException exception)
            {
                bool tail = index == lines.Length - 1 || lines.Skip(index + 1).All(string.IsNullOrEmpty);
                if (discoveredId != expectedThreadId)
                {
                    return Absent(expectedThreadId);
                }

                return Result(
                    tail ? CodexRolloutReadStatus.Partial : CodexRolloutReadStatus.Corrupt,
                    expectedThreadId, path, records, content, verifiedLine,
                    omissions.Append(tail ? "truncated-tail" : "malformed-middle").ToArray(),
                    exception.GetType().Name);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                discoveredId ??= ReadSessionId(root);
                if (discoveredId is not null && discoveredId != expectedThreadId)
                {
                    return Absent(expectedThreadId);
                }

                SessionContentRecord? record = ReadPublicRecord(root, records.Count);
                if (record is not null)
                {
                    records.Add(record);
                }
                else if (ContainsHiddenReasoning(root))
                {
                    omissions.Add("hidden-reasoning");
                }

                verifiedLine = index + 1;
            }
        }

        if (discoveredId != expectedThreadId)
        {
            return Absent(expectedThreadId);
        }

        return Result(CodexRolloutReadStatus.Complete, expectedThreadId, path, records, content,
            verifiedLine, omissions.ToArray(), null);
    }

    private static string? ReadSessionId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("session_meta", out JsonElement direct) && direct.ValueKind == JsonValueKind.Object)
        {
            return String(direct, "id");
        }

        string? type = String(root, "type");
        if (type == "session_meta" && root.TryGetProperty("payload", out JsonElement payload))
        {
            return String(payload, "id");
        }

        return null;
    }

    private static SessionContentRecord? ReadPublicRecord(JsonElement root, int order)
    {
        string? type = String(root, "type");
        JsonElement payload = root.TryGetProperty("payload", out JsonElement value) ? value : root;
        if (type == "response_item" && String(payload, "type") == "message")
        {
            string role = String(payload, "role") ?? "unknown";
            if (role is not ("user" or "assistant"))
            {
                return null;
            }

            string text = ReadContentText(payload);
            return text.Length == 0 ? null : new SessionContentRecord(
                order, "message", role, text, String(payload, "id"), new Dictionary<string, string>());
        }

        if (type == "event_msg" && String(payload, "type") == "agent_message")
        {
            string? text = String(payload, "message");
            return string.IsNullOrWhiteSpace(text) ? null : new SessionContentRecord(
                order, "message", "assistant", text, null, new Dictionary<string, string>());
        }

        if (type == "compacted" || String(payload, "type") == "compaction")
        {
            string? text = String(payload, "summary");
            return string.IsNullOrWhiteSpace(text) ? null : new SessionContentRecord(
                order, "compaction", "system-summary", text, null, new Dictionary<string, string>());
        }

        string? payloadType = String(payload, "type");
        if (type == "response_item" && payloadType is
            "function_call" or "function_call_output" or "local_shell_call" or "custom_tool_call")
        {
            return new SessionContentRecord(
                order, "tool-summary", "tool-summary", $"{payloadType}:recorded",
                String(payload, "id"), new Dictionary<string, string>());
        }

        if (type == "event_msg" && payloadType is
            "exec_command_begin" or "exec_command_end" or "patch_apply_begin" or "patch_apply_end")
        {
            return new SessionContentRecord(
                order, "tool-summary", "tool-summary", $"{payloadType}:recorded",
                null, new Dictionary<string, string>());
        }

        return null;
    }

    private static string ReadContentText(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
        {
            return String(payload, "text") ?? string.Empty;
        }

        return string.Concat(content.EnumerateArray()
            .Where(part => String(part, "type") is "input_text" or "output_text" or "text")
            .Select(part => String(part, "text"))
            .Where(text => text is not null));
    }

    private static bool ContainsHiddenReasoning(JsonElement root) =>
        root.GetRawText().Contains("encrypted_content", StringComparison.Ordinal)
        || string.Equals(String(root, "type"), "reasoning", StringComparison.Ordinal);

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static CodexRolloutReadResult Absent(string threadId) =>
        new(CodexRolloutReadStatus.Absent, threadId, null, [], null, null, [], null);

    private static CodexRolloutReadResult Result(
        CodexRolloutReadStatus status,
        string threadId,
        string path,
        IReadOnlyList<SessionContentRecord> records,
        string content,
        int verifiedLine,
        IReadOnlyList<string> omissions,
        string? diagnostic) =>
        new(status, threadId, path, records,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
            $"line:{verifiedLine}", omissions.Order(StringComparer.Ordinal).ToArray(), diagnostic);
}

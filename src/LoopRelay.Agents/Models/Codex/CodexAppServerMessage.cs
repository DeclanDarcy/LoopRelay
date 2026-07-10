using System.Text.Json;
using LoopRelay.Agents.Primitives.Codex;

namespace LoopRelay.Agents.Models.Codex;


/// <summary>
/// A parsed inbound Codex app-server JSON-RPC message. Distinguishes the three server-to-client
/// shapes so the session can correlate responses by id, route notifications to the active turn,
/// and answer server requests (approvals).
/// </summary>
public sealed record CodexAppServerMessage(
    CodexAppServerMessageKind Kind,
    object? Id,
    string? Method,
    JsonElement Params,
    JsonElement Result,
    string? ErrorMessage,
    int? ErrorCode,
    JsonElement ErrorData,
    JsonElement CompleteResponse,
    bool ParseIntegrity)
{
    /// <summary>The id as a number when the server used one (our outbound ids are numbers), else null.</summary>
    public long? NumericId => Id as long?;

    public static CodexAppServerMessage Parse(string line)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unknown();
            }

            object? id = ReadId(root);
            string? method = root.TryGetProperty("method", out JsonElement m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            JsonElement @params = CloneOrDefault(root, "params");
            JsonElement result = CloneOrDefault(root, "result");
            string? error = ReadErrorMessage(root);
            int? errorCode = ReadErrorCode(root);
            JsonElement errorData = ReadErrorData(root);

            CodexAppServerMessageKind kind = method is not null
                ? (id is not null ? CodexAppServerMessageKind.ServerRequest : CodexAppServerMessageKind.Notification)
                : (id is not null && (result.ValueKind != JsonValueKind.Undefined || HasError(root))
                    ? CodexAppServerMessageKind.Response
                    : CodexAppServerMessageKind.Unknown);

            return new CodexAppServerMessage(
                kind, id, method, @params, result, error, errorCode, errorData, root.Clone(), ParseIntegrity: true);
        }
        catch (JsonException)
        {
            return Unknown();
        }
    }

    private static CodexAppServerMessage Unknown() =>
        new(CodexAppServerMessageKind.Unknown, null, null, default, default, null, null, default, default, ParseIntegrity: false);

    private static object? ReadId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out JsonElement id))
        {
            return null;
        }

        return id.ValueKind switch
        {
            JsonValueKind.Number when id.TryGetInt64(out long value) => value,
            JsonValueKind.String => id.GetString(),
            _ => null
        };
    }

    private static string? ReadErrorMessage(JsonElement root) =>
        root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String
            ? message.GetString()
            : null;

    private static int? ReadErrorCode(JsonElement root) =>
        root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.Number
        && code.TryGetInt32(out int value)
            ? value
            : null;

    private static JsonElement ReadErrorData(JsonElement root) =>
        root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("data", out JsonElement data)
            ? data.Clone()
            : default;

    private static bool HasError(JsonElement root) =>
        root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.Object;

    private static JsonElement CloneOrDefault(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.Clone() : default;
}

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Abstractions.Evaluation;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Codex;
using LoopRelay.Permissions.Models.Evaluation;
using LoopRelay.Permissions.Primitives;
using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Services.Codex;

public sealed class CodexPermissionAdapter : IPermissionAdapter
{
    public const string AcceptDecision = "accept";
    public const string DenialDecision = "decline";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public PermissionRequest Parse(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory)
    {
        PermissionEnvelope envelope = JsonSerializer.Deserialize<PermissionEnvelope>(payload, Options)
            ?? throw new InvalidOperationException("Codex approval request was empty.");

        (string requestId, bool requestIdIsString) = ReadId(envelope);
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new InvalidOperationException("Codex approval request did not include id.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Method))
        {
            throw new InvalidOperationException("Codex approval request did not include method.");
        }

        if (envelope.Params is null)
        {
            throw new InvalidOperationException("Codex approval request did not include params.");
        }

        (string toolName, string? rawCommand, PermissionRequestDetails details) = envelope.Method switch
        {
            CodexProtocolMethods.CommandExecutionRequestApproval => ParseCommandApproval(envelope.Params),
            CodexProtocolMethods.FileChangeRequestApproval => ParseFileChangeApproval(envelope.Params),
            CodexProtocolMethods.ToolRequestUserInput => ParseToolRequestUserInput(envelope.Params),
            CodexProtocolMethods.PermissionsRequestApproval => ParseGenericRequest("permissions", envelope.Params),
            CodexProtocolMethods.DynamicToolCall => ParseGenericRequest("toolCall", envelope.Params),
            CodexProtocolMethods.McpServerElicitationRequest => ParseGenericRequest("mcpServerElicitation", envelope.Params),
            _ => throw new InvalidOperationException($"Unknown Codex approval method '{envelope.Method}'."),
        };

        return new PermissionRequest(
            requestId,
            toolName,
            rawCommand,
            repoIdentity,
            workingDirectory,
            requestIdIsString,
            details);
    }

    public byte[] BuildResponse(PermissionRequest request, PermissionResult result) =>
        BuildResponse(request.RequestId, request.RequestIdIsString, result);

    public byte[] BuildResponse(string requestId, PermissionResult result) =>
        BuildResponse(requestId, requestIdIsString: !long.TryParse(requestId, out _), result);

    private static byte[] BuildResponse(string requestId, bool requestIdIsString, PermissionResult result)
    {
        string decision = result.Decision == RuleDecision.Allow ? AcceptDecision : DenialDecision;
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestIdIsString ? requestId : ParseId(requestId),
            ["result"] = new Dictionary<string, object?> { ["decision"] = decision },
        };

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonSerializer.Serialize(writer, response, Options);
        }

        byte[] bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static (string ToolName, string? RawCommand, PermissionRequestDetails Details) ParseCommandApproval(JsonNode parameters)
    {
        CommandExecutionApprovalRequest request = parameters.Deserialize<CommandExecutionApprovalRequest>(Options)
            ?? throw new InvalidOperationException("Invalid command approval params.");

        var details = new PermissionRequestDetails(
            PermissionRequestKind.CommandExecution,
            CodexProtocolMethods.CommandExecutionRequestApproval,
            Command: request.Command,
            Cwd: request.Cwd,
            RequestsNetwork: request.NetworkApprovalContext is not null);

        if (!string.IsNullOrWhiteSpace(request.Command))
        {
            return ("Bash", request.Command, details);
        }

        if (request.NetworkApprovalContext is not null)
        {
            return ("networkAccess", null, details);
        }

        return ("commandExecution", request.Reason, details);
    }

    private static (string ToolName, string? RawCommand, PermissionRequestDetails Details) ParseFileChangeApproval(JsonNode parameters)
    {
        FileChangeApprovalRequest request = parameters.Deserialize<FileChangeApprovalRequest>(Options)
            ?? throw new InvalidOperationException("Invalid file-change approval params.");

        string? filePath = request.TargetPath
            ?? request.Path
            ?? request.FilePath
            ?? request.File;
        string? operation = request.Operation ?? request.Kind ?? request.Type;
        PermissionPathAccess access = ClassifyFileAccess(operation);

        string raw = request.GrantRoot is null
            ? $"codex_file_change {request.ItemId}"
            : $"codex_file_change {QuoteCommandArgument(request.GrantRoot)}";

        return ("fileChange", raw, new PermissionRequestDetails(
            PermissionRequestKind.FileChange,
            CodexProtocolMethods.FileChangeRequestApproval,
            FileOperation: operation,
            FilePath: filePath,
            GrantRoot: request.GrantRoot,
            PathArguments: request.TargetPaths ?? [],
            PathAccess: access));
    }

    private static (string ToolName, string? RawCommand, PermissionRequestDetails Details) ParseToolRequestUserInput(JsonNode parameters)
    {
        ToolRequestUserInputRequest request = parameters.Deserialize<ToolRequestUserInputRequest>(Options)
            ?? throw new InvalidOperationException("Invalid user-input request params.");

        string raw = string.Join(
            " | ",
            request.Questions.Select(question => $"{question.Id}: {question.Question}"));

        return ("requestUserInput", raw, new PermissionRequestDetails(
            PermissionRequestKind.UserInput,
            CodexProtocolMethods.ToolRequestUserInput));
    }

    private static (string ToolName, string? RawCommand, PermissionRequestDetails Details) ParseGenericRequest(
        string toolName,
        JsonNode parameters)
    {
        if (string.Equals(toolName, "toolCall", StringComparison.Ordinal))
        {
            string? requestedTool = ReadString(parameters, "name")
                ?? ReadString(parameters, "toolName")
                ?? ReadString(parameters, "tool")
                ?? ReadString(parameters, "command");
            JsonNode? arguments = ReadNode(parameters, "arguments")
                ?? ReadNode(parameters, "args")
                ?? ReadNode(parameters, "input")
                ?? parameters;
            IReadOnlyList<string> pathArguments = ExtractPathArguments(arguments);
            PermissionPathAccess access = ClassifyToolAccess(requestedTool);

            return (toolName, parameters.ToJsonString(Options), new PermissionRequestDetails(
                PermissionRequestKind.ToolCall,
                CodexProtocolMethods.DynamicToolCall,
                ToolName: requestedTool,
                PathArguments: pathArguments,
                PathAccess: access));
        }

        PermissionRequestKind kind = toolName switch
        {
            "permissions" => PermissionRequestKind.Permissions,
            "mcpServerElicitation" => PermissionRequestKind.McpElicitation,
            _ => PermissionRequestKind.Unknown,
        };

        return (toolName, parameters.ToJsonString(Options), new PermissionRequestDetails(
            kind,
            kind == PermissionRequestKind.McpElicitation
                ? CodexProtocolMethods.McpServerElicitationRequest
                : CodexProtocolMethods.PermissionsRequestApproval));
    }

    private static PermissionPathAccess ClassifyFileAccess(string? operation)
    {
        if (operation is null)
        {
            return PermissionPathAccess.Write;
        }

        if (operation.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || operation.Contains("remove", StringComparison.OrdinalIgnoreCase))
        {
            return PermissionPathAccess.Delete;
        }

        return PermissionPathAccess.Write;
    }

    private static PermissionPathAccess ClassifyToolAccess(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return PermissionPathAccess.Unknown;
        }

        string normalized = toolName.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("remove", StringComparison.OrdinalIgnoreCase))
        {
            return PermissionPathAccess.Delete;
        }

        if (normalized.Contains("write", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("edit", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("patch", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("replace", StringComparison.OrdinalIgnoreCase))
        {
            return PermissionPathAccess.Write;
        }

        if (normalized.Contains("read", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("list", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("grep", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("find", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("search", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("open", StringComparison.OrdinalIgnoreCase))
        {
            return PermissionPathAccess.Read;
        }

        return PermissionPathAccess.Unknown;
    }

    private static IReadOnlyList<string> ExtractPathArguments(JsonNode? node)
    {
        var paths = new List<string>();
        ExtractPathArguments(node, parentName: null, paths);
        return paths;
    }

    private static void ExtractPathArguments(JsonNode? node, string? parentName, List<string> paths)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonValue value && value.TryGetValue(out string? stringValue))
        {
            if (LooksPathBearingKey(parentName) || LooksLikePath(stringValue))
            {
                paths.Add(stringValue);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                ExtractPathArguments(child, parentName, paths);
            }

            return;
        }

        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> property in obj)
            {
                ExtractPathArguments(property.Value, property.Key, paths);
            }
        }
    }

    private static bool LooksPathBearingKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("path", StringComparison.OrdinalIgnoreCase)
            || name.Contains("file", StringComparison.OrdinalIgnoreCase)
            || name.Contains("dir", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "cwd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "directory", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.StartsWith(".", StringComparison.Ordinal)
            || value.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonNode node, string propertyName)
    {
        JsonNode? value = ReadNode(node, propertyName);
        return value is JsonValue jsonValue && jsonValue.TryGetValue(out string? stringValue)
            ? stringValue
            : null;
    }

    private static JsonNode? ReadNode(JsonNode node, string propertyName) =>
        node is JsonObject obj && obj.TryGetPropertyValue(propertyName, out JsonNode? value)
            ? value
            : null;

    private static object ParseId(string requestId) =>
        long.TryParse(requestId, out long numericId) ? numericId : requestId;

    private static (string Id, bool IsString) ReadId(PermissionEnvelope envelope)
    {
        if (envelope.Id is null)
        {
            return (string.Empty, IsString: false);
        }

        return envelope.Id.GetValueKind() switch
        {
            JsonValueKind.Number => (envelope.Id.GetValue<long>().ToString(System.Globalization.CultureInfo.InvariantCulture), IsString: false),
            JsonValueKind.String => (envelope.Id.GetValue<string>() ?? string.Empty, IsString: true),
            _ => (string.Empty, IsString: false),
        };
    }

    private static string QuoteCommandArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) || argument.Contains('"', StringComparison.Ordinal)
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
    }

    private sealed class PermissionEnvelope
    {
        public JsonNode? Id { get; init; }

        public string? Method { get; init; }

        public JsonNode? Params { get; init; }
    }

    private sealed class CommandExecutionApprovalRequest
    {
        public string? Reason { get; init; }

        public string? Command { get; init; }

        public string? Cwd { get; init; }

        public string? Shell { get; init; }

        public JsonNode? NetworkApprovalContext { get; init; }
    }

    private sealed class FileChangeApprovalRequest
    {
        public string? ItemId { get; init; }

        public string? GrantRoot { get; init; }

        public string? Operation { get; init; }

        public string? Kind { get; init; }

        public string? Type { get; init; }

        public string? TargetPath { get; init; }

        public IReadOnlyList<string>? TargetPaths { get; init; }

        public string? Path { get; init; }

        public string? FilePath { get; init; }

        public string? File { get; init; }
    }

    private sealed class ToolRequestUserInputRequest
    {
        public IReadOnlyList<ToolRequestUserInputQuestion> Questions { get; init; } = [];
    }

    private sealed class ToolRequestUserInputQuestion
    {
        public string Id { get; init; } = string.Empty;

        public string Question { get; init; } = string.Empty;
    }
}

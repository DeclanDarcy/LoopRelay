using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Codex;

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

        (string toolName, string? rawCommand) = envelope.Method switch
        {
            CodexProtocolMethods.CommandExecutionRequestApproval => ParseCommandApproval(envelope.Params),
            CodexProtocolMethods.FileChangeRequestApproval => ParseFileChangeApproval(envelope.Params),
            CodexProtocolMethods.ToolRequestUserInput => ParseToolRequestUserInput(envelope.Params),
            CodexProtocolMethods.PermissionsRequestApproval => ParseGenericRequest("permissions", envelope.Params),
            CodexProtocolMethods.DynamicToolCall => ParseGenericRequest("toolCall", envelope.Params),
            CodexProtocolMethods.McpServerElicitationRequest => ParseGenericRequest("mcpServerElicitation", envelope.Params),
            _ => throw new InvalidOperationException($"Unknown Codex approval method '{envelope.Method}'."),
        };

        return new PermissionRequest(requestId, toolName, rawCommand, repoIdentity, workingDirectory, requestIdIsString);
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

    private static (string ToolName, string? RawCommand) ParseCommandApproval(JsonNode parameters)
    {
        CommandExecutionApprovalRequest request = parameters.Deserialize<CommandExecutionApprovalRequest>(Options)
            ?? throw new InvalidOperationException("Invalid command approval params.");

        if (!string.IsNullOrWhiteSpace(request.Command))
        {
            return ("Bash", request.Command);
        }

        if (request.NetworkApprovalContext is not null)
        {
            return ("networkAccess", null);
        }

        return ("commandExecution", request.Reason);
    }

    private static (string ToolName, string? RawCommand) ParseFileChangeApproval(JsonNode parameters)
    {
        FileChangeApprovalRequest request = parameters.Deserialize<FileChangeApprovalRequest>(Options)
            ?? throw new InvalidOperationException("Invalid file-change approval params.");

        string raw = request.GrantRoot is null
            ? $"codex_file_change {request.ItemId}"
            : $"codex_file_change {QuoteCommandArgument(request.GrantRoot)}";

        return ("fileChange", raw);
    }

    private static (string ToolName, string? RawCommand) ParseToolRequestUserInput(JsonNode parameters)
    {
        ToolRequestUserInputRequest request = parameters.Deserialize<ToolRequestUserInputRequest>(Options)
            ?? throw new InvalidOperationException("Invalid user-input request params.");

        string raw = string.Join(
            " | ",
            request.Questions.Select(question => $"{question.Id}: {question.Question}"));

        return ("requestUserInput", raw);
    }

    private static (string ToolName, string? RawCommand) ParseGenericRequest(string toolName, JsonNode parameters) =>
        (toolName, parameters.ToJsonString(Options));

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

        public JsonNode? NetworkApprovalContext { get; init; }
    }

    private sealed class FileChangeApprovalRequest
    {
        public string? ItemId { get; init; }

        public string? GrantRoot { get; init; }
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

using System.Text;
using System.Text.Json;
using LoopRelay.Permissions.Codex;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Tests;

public sealed class CodexPermissionAdapterTests
{
    private readonly CodexPermissionAdapter adapter = new();

    [Fact]
    public void Parses_command_approval_and_preserves_numeric_id_as_string()
    {
        PermissionRequest request = adapter.Parse(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":42,"method":"item/commandExecution/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","command":"dotnet build","cwd":"C:\\repo"}}"""),
            "repo",
            "C:\\repo");

        Assert.Equal("42", request.RequestId);
        Assert.Equal("Bash", request.ToolName);
        Assert.Equal("dotnet build", request.RawCommand);
    }

    [Fact]
    public void Parses_file_change_as_synthetic_command_that_the_closed_world_policy_denies()
    {
        PermissionRequest request = adapter.Parse(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":"file-1","method":"item/fileChange/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"item-2","grantRoot":"C:\\repo\\src"}}"""),
            "repo",
            "C:\\repo");

        Assert.Equal("file-1", request.RequestId);
        Assert.Equal("fileChange", request.ToolName);
        Assert.Contains("codex_file_change", request.RawCommand, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("item/permissions/requestApproval", "permissions")]
    [InlineData("item/tool/call", "toolCall")]
    [InlineData("mcpServer/elicitation/request", "mcpServerElicitation")]
    public void Parses_known_future_requests_as_generic_closed_world_tools(string method, string expectedTool)
    {
        string json = "{\"jsonrpc\":\"2.0\",\"id\":\"req-9\",\"method\":\"" +
            method +
            "\",\"params\":{\"name\":\"future\"}}";

        PermissionRequest request = adapter.Parse(
            Encoding.UTF8.GetBytes(json),
            "repo",
            "/repo");

        Assert.Equal("req-9", request.RequestId);
        Assert.Equal(expectedTool, request.ToolName);
        Assert.Contains("future", request.RawCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void Parses_user_input_requests_as_denied_summaries()
    {
        PermissionRequest request = adapter.Parse(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":"input-1","method":"item/tool/requestUserInput","params":{"threadId":"t","turnId":"u","itemId":"i","questions":[{"id":"mode","header":"Mode","question":"Choose mode?"}]}}"""),
            "repo",
            "/repo");

        Assert.Equal("requestUserInput", request.ToolName);
        Assert.Contains("Choose mode?", request.RawCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void Builds_complete_jsonrpc_response_with_pinned_decline_spelling()
    {
        byte[] response = adapter.BuildResponse("45", new PermissionResult(RuleDecision.Deny, "blocked"));

        Assert.Equal((byte)'\n', response[^1]);
        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(45, root.GetProperty("id").GetInt64());
        Assert.Equal(CodexPermissionAdapter.DenialDecision, root.GetProperty("result").GetProperty("decision").GetString());
        Assert.Equal("decline", root.GetProperty("result").GetProperty("decision").GetString());
    }

    [Fact]
    public void Builds_accept_response_for_allow_and_preserves_string_ids()
    {
        byte[] response = adapter.BuildResponse("req-9", new PermissionResult(RuleDecision.Allow, "safe"));

        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;
        Assert.Equal("req-9", root.GetProperty("id").GetString());
        Assert.Equal("accept", root.GetProperty("result").GetProperty("decision").GetString());
    }

    [Fact]
    public void Gateway_response_preserves_numeric_looking_string_ids()
    {
        PermissionRequest request = adapter.Parse(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":"45","method":"item/commandExecution/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","command":"dotnet build"}}"""),
            "repo",
            "/repo");

        byte[] response = adapter.BuildResponse(request, new PermissionResult(RuleDecision.Allow, "safe"));

        using JsonDocument document = JsonDocument.Parse(response);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("id").ValueKind);
        Assert.Equal("45", document.RootElement.GetProperty("id").GetString());
    }
}

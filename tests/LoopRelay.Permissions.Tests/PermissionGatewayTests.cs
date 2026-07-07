using System.Text;
using System.Text.Json;
using LoopRelay.Permissions.Codex;
using LoopRelay.Permissions.Services;

namespace LoopRelay.Permissions.Tests;

public sealed class PermissionGatewayTests
{
    [Fact]
    public void Safe_command_approval_produces_accept()
    {
        byte[] response = Gateway().Evaluate(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":77,"method":"item/commandExecution/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","command":"dotnet test"}}"""),
            "repo",
            "/repo");

        Assert.Equal("accept", Decision(response));
    }

    [Fact]
    public void Dangerous_command_approval_produces_protocol_decline()
    {
        byte[] response = Gateway().Evaluate(
            Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":"deny-1","method":"item/commandExecution/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","command":"git push"}}"""),
            "repo",
            "/repo");

        Assert.Equal(CodexPermissionAdapter.DenialDecision, Decision(response));
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":"net-1","method":"item/commandExecution/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","networkApprovalContext":{"host":"example.com"}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":"file-1","method":"item/fileChange/requestApproval","params":{"threadId":"t","turnId":"u","itemId":"i","grantRoot":"/repo/src"}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":"input-1","method":"item/tool/requestUserInput","params":{"threadId":"t","turnId":"u","itemId":"i","questions":[{"id":"q","header":"Q","question":"Proceed?"}]}}""")]
    public void Network_file_change_and_user_input_requests_decline_by_default(string request)
    {
        byte[] response = Gateway().Evaluate(
            Encoding.UTF8.GetBytes(request),
            "repo",
            "/repo");

        Assert.Equal(CodexPermissionAdapter.DenialDecision, Decision(response));
    }

    private static PermissionGateway Gateway() =>
        new(
            new CodexPermissionAdapter(),
            new PermissionHandler(
                new CommandParser(),
                new CommandCanonicalizer(),
                new Sha256FingerprintService(),
                new InMemoryPermissionCache(),
                new PermissionEvaluatorEngine(),
                new InvariantGuard()));

    private static string Decision(byte[] response)
    {
        using JsonDocument document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("result").GetProperty("decision").GetString()!;
    }
}

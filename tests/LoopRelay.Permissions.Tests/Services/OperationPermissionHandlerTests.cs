using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Services;

namespace LoopRelay.Permissions.Tests;

public sealed class OperationPermissionHandlerTests
{
    private static readonly OperationPermissionHandler Handler = new();

    [Fact]
    public void Allows_exact_write_path()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            FileChange("write", ".agents/details.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Allows_glob_write_path()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            FileChange("write", ".agents/milestones/m1.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Allows_exact_read_tool_path()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            ToolCall(PermissionPathAccess.Read, ".agents/plan.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Allows_glob_read_tool_path()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            ToolCall(PermissionPathAccess.Read, ".agents/specs/epic.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Denies_repository_escape()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            ToolCall(PermissionPathAccess.Read, "../outside.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Deny, result.Decision);
    }

    [Fact]
    public void Denies_delete_even_inside_write_profile()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            FileChange("delete", ".agents/details.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Deny, result.Decision);
    }

    [Fact]
    public void Denies_global_safe_commands_inside_operation_scope()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            new PermissionRequest(
                "1",
                "Bash",
                "dotnet test",
                "repo",
                repo.Root,
                Details: new PermissionRequestDetails(
                    PermissionRequestKind.CommandExecution,
                    "item/commandExecution/requestApproval",
                    Command: "dotnet test")),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Deny, result.Decision);
    }

    [Fact]
    public void Denies_network_user_input_and_mcp_requests()
    {
        using TempRepo repo = TempRepo.Create();
        OperationPermissionProfile profile = Profile(repo.Root);

        Assert.Equal(RuleDecision.Deny, Handler.Evaluate(
            new PermissionRequest(
                "1",
                "networkAccess",
                null,
                "repo",
                repo.Root,
                Details: new PermissionRequestDetails(
                    PermissionRequestKind.CommandExecution,
                    "item/commandExecution/requestApproval",
                    RequestsNetwork: true)),
            profile).Decision);

        Assert.Equal(RuleDecision.Deny, Handler.Evaluate(
            new PermissionRequest(
                "2",
                "requestUserInput",
                null,
                "repo",
                repo.Root,
                Details: new PermissionRequestDetails(
                    PermissionRequestKind.UserInput,
                    "item/tool/requestUserInput")),
            profile).Decision);

        Assert.Equal(RuleDecision.Deny, Handler.Evaluate(
            new PermissionRequest(
                "3",
                "mcpServerElicitation",
                null,
                "repo",
                repo.Root,
                Details: new PermissionRequestDetails(
                    PermissionRequestKind.McpElicitation,
                    "mcpServer/elicitation/request")),
            profile).Decision);
    }

    [Fact]
    public void Rejects_broad_grant_root_that_exceeds_profile()
    {
        using TempRepo repo = TempRepo.Create();
        PermissionResult result = Handler.Evaluate(
            new PermissionRequest(
                "1",
                "fileChange",
                $"codex_file_change {repo.Root}",
                "repo",
                repo.Root,
                Details: new PermissionRequestDetails(
                    PermissionRequestKind.FileChange,
                    "item/fileChange/requestApproval",
                    GrantRoot: repo.Root,
                    PathAccess: PermissionPathAccess.Write)),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Deny, result.Decision);
    }

    private static OperationPermissionProfile Profile(string root) =>
        new(
            "test",
            root,
            [".agents/plan.md"],
            [new OperationPathGlob(".agents/specs", "*.md")],
            [".agents/details.md"],
            [new OperationPathGlob(".agents/milestones", "m*.md")]);

    private static PermissionRequest FileChange(string operation, string path) =>
        new(
            "1",
            "fileChange",
            $"codex_file_change {path}",
            "repo",
            ".",
            Details: new PermissionRequestDetails(
                PermissionRequestKind.FileChange,
                "item/fileChange/requestApproval",
                FileOperation: operation,
                FilePath: path,
                PathAccess: operation == "delete" ? PermissionPathAccess.Delete : PermissionPathAccess.Write));

    private static PermissionRequest ToolCall(PermissionPathAccess access, string path) =>
        new(
            "1",
            "toolCall",
            path,
            "repo",
            ".",
            Details: new PermissionRequestDetails(
                PermissionRequestKind.ToolCall,
                "item/tool/call",
                ToolName: access == PermissionPathAccess.Read ? "read" : "write",
                PathArguments: [path],
                PathAccess: access));

    private sealed class TempRepo : IDisposable
    {
        private TempRepo(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempRepo Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "looprelay-permissions-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempRepo(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}

using System.Diagnostics;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Evaluation;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives;
using LoopRelay.Permissions.Primitives.Requests;
using LoopRelay.Permissions.Services;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Tests.Services;

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

    [Fact]
    public void Allows_write_path_when_segments_do_not_exist_on_disk()
    {
        using TempRepo repo = TempRepo.Create();

        // Nothing under repo.Root is created on disk: every path segment is absent.
        PermissionResult result = Handler.Evaluate(
            FileChange("write", ".agents/details.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Allows_write_path_through_real_directory_chain_without_reparse_points()
    {
        using TempRepo repo = TempRepo.Create();
        Directory.CreateDirectory(Path.Combine(repo.Root, ".agents"));
        File.WriteAllText(Path.Combine(repo.Root, ".agents", "details.md"), "content");

        PermissionResult result = Handler.Evaluate(
            FileChange("write", ".agents/details.md"),
            Profile(repo.Root));

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Denies_write_path_through_directory_junction_reparse_point()
    {
        using TempRepo repo = TempRepo.Create();
        string realTarget = Path.Combine(
            Path.GetTempPath(),
            "looprelay-permissions-tests-junction-target",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(realTarget);

        string junctionPath = Path.Combine(repo.Root, ".agents");

        if (!TryCreateDirectoryJunction(junctionPath, realTarget, out string? failureReason))
        {
            Directory.Delete(realTarget, recursive: true);

            // Reparse-point creation can require elevated privilege / Developer Mode in some
            // sandboxes. Treat only an access/privilege style failure as an environment
            // limitation (not a code defect); anything else fails the test loudly.
            bool looksLikePrivilegeIssue = failureReason is not null
                && (failureReason.Contains("privilege", StringComparison.OrdinalIgnoreCase)
                    || failureReason.Contains("denied", StringComparison.OrdinalIgnoreCase)
                    || failureReason.Contains("not authorized", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                looksLikePrivilegeIssue,
                $"Junction creation failed for an unexpected reason (not an access/privilege issue): {failureReason}");
            return;
        }

        try
        {
            PermissionResult result = Handler.Evaluate(
                FileChange("write", ".agents/details.md"),
                Profile(repo.Root));

            Assert.Equal(RuleDecision.Deny, result.Decision);
        }
        finally
        {
            Directory.Delete(junctionPath);
            Directory.Delete(realTarget, recursive: true);
        }
    }

    private static bool TryCreateDirectoryJunction(string junctionPath, string targetPath, out string? failureReason)
    {
        failureReason = null;
        try
        {
            var startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                failureReason = "Unable to start mklink process.";
                return false;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !Directory.Exists(junctionPath))
            {
                failureReason = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            failureReason = ex.Message;
            return false;
        }
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

using LoopRelay.Cli.Services.Cli;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Resolution;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public class CliArgumentsTests
{
    [Fact]
    public void TryParse_CollectsRepeatedPolicyOverridesAsExplicitFlagInputs()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-policy").FullName;

        bool ok = CliArguments.TryParse(
            ["--repo", dir, "--policy", "execution.maxNoChangesCommits=3", "--policy", "decisions.sessionResume=false"],
            out UnifiedCliInvocation invocation,
            out string error);

        Assert.True(ok, error);
        Assert.NotNull(invocation.PolicyOverrides);
        Assert.Equal(2, invocation.PolicyOverrides.Count);
        PolicyOverride first = invocation.PolicyOverrides[0];
        Assert.Equal("execution.maxNoChangesCommits", first.Key);
        Assert.Equal("3", first.Value);
        Assert.Equal("flag:--policy", first.Origin);
        Assert.True(first.IsExplicit);
        Assert.Equal("decisions.sessionResume", invocation.PolicyOverrides[1].Key);
        Assert.Equal("false", invocation.PolicyOverrides[1].Value);
    }

    [Theory]
    [InlineData("no-equals-sign")]
    [InlineData("=value")]
    [InlineData("key=")]
    public void TryParse_RejectsMalformedPolicyOverrides(string assignment)
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-policy-bad").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, "--policy", assignment], out _, out string error);

        Assert.False(ok);
        Assert.Contains("--policy", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_RejectsPolicyFlagWithoutAValue()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-policy-missing").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, "--policy"], out _, out string error);

        Assert.False(ok);
        Assert.Contains("--policy", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_WithExistingDirectory_ReturnsRepositoryWithAbsolutePath()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-args").FullName;

        bool ok = CliArguments.TryParse(new[] { dir }, out var repository, out string error);

        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(dir), repository.Path);
        Assert.Equal(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), repository.Name);
        Assert.NotEqual(Guid.Empty, repository.Id);
    }

    [Fact]
    public void TryParse_WithNoArgs_UsesCurrentDirectoryDefaultRepository()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-default-repo").FullName;

        bool ok = CliArguments.TryParse(Array.Empty<string>(), out UnifiedCliInvocation invocation, out string error, dir);

        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(dir), invocation.Repository.Path);
        Assert.Equal(InvocationModeKind.DefaultChained, invocation.WorkflowInvocation.Mode);
        Assert.Equal(UnifiedCliCommandKind.Run, invocation.Command.Kind);
    }

    [Fact]
    public void TryParse_WithMissingDirectory_Fails()
    {
        bool ok = CliArguments.TryParse(new[] { "Z:/does/not/exist/cc-cli" }, out _, out string error);

        Assert.False(ok);
        Assert.Contains("does not exist", error);
    }

    [Theory]
    [InlineData("--eval", InvocationModeKind.ForcedEvalChain)]
    [InlineData("--traditional", InvocationModeKind.ForcedTraditionalChain)]
    public void TryParse_WithForcedChainFlags_SelectsChainedMode(string flag, InvocationModeKind expected)
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-forced-mode").FullName;

        bool ok = CliArguments.TryParse([flag, "--repo", dir], out UnifiedCliInvocation invocation, out string error);

        Assert.True(ok, error);
        Assert.Equal(expected, invocation.WorkflowInvocation.Mode);
        Assert.Equal(UnifiedCliCommandKind.Run, invocation.Command.Kind);
    }

    [Theory]
    [InlineData("eval", InvocationModeKind.BoundedEval)]
    [InlineData("traditional", InvocationModeKind.BoundedTraditional)]
    [InlineData("plan", InvocationModeKind.BoundedPlan)]
    [InlineData("execute", InvocationModeKind.BoundedExecute)]
    public void TryParse_WithBoundedWorkflowCommand_SelectsBoundedMode(string command, InvocationModeKind expected)
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-bounded-mode").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, command], out UnifiedCliInvocation invocation, out string error);

        Assert.True(ok, error);
        Assert.Equal(expected, invocation.WorkflowInvocation.Mode);
        Assert.Equal(UnifiedCliCommandKind.Run, invocation.Command.Kind);
    }

    [Theory]
    [InlineData("status", "Status")]
    [InlineData("storage init", "StorageInit")]
    [InlineData("storage import", "StorageImport")]
    [InlineData("storage export", "StorageExport")]
    [InlineData("storage sync", "StorageSync")]
    [InlineData("storage verify", "StorageVerify")]
    public void TryParse_WithOperationalCommand_SelectsCommand(string commandText, string expected)
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-operational-command").FullName;
        string[] command = commandText.Split(' ');

        bool ok = CliArguments.TryParse(["--repo", dir, .. command], out UnifiedCliInvocation invocation, out string error);

        Assert.True(ok, error);
        Assert.Equal(expected, invocation.Command.Kind.ToString());
    }

    [Fact]
    public void TryParse_RejectsRetiredUnblockCommand()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-retired-unblock").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, "unblock"], out UnifiedCliInvocation _, out string error);

        Assert.False(ok);
        Assert.Contains("Unknown command: unblock", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_WithConflictingForcedChainFlags_Fails()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-conflicting-flags").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, "--eval", "--traditional"], out UnifiedCliInvocation _, out string error);

        Assert.False(ok);
        Assert.Contains("cannot be used together", error);
    }

    [Fact]
    public void TryParse_WithForcedChainFlagConflictingWithBoundedCommand_Fails()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-conflicting-bounded").FullName;

        bool ok = CliArguments.TryParse(["--repo", dir, "--eval", "traditional"], out UnifiedCliInvocation _, out string error);

        Assert.False(ok);
        Assert.Contains("can only be combined", error);
    }
}

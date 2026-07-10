using LoopRelay.Cli.Services.Cli;
using LoopRelay.Orchestration.Resolution;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public class CliArgumentsTests
{
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
    [InlineData("unblock", "Unblock")]
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

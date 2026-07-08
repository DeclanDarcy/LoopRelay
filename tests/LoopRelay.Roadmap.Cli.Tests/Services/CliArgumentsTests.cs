using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryParse_requires_repo_dir()
    {
        Assert.False(CliArguments.TryParse([], out RoadmapCliInvocation _, out string error));
        Assert.Contains("REPO_DIR", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_existing_repo_dir()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([repo.Root], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.Run, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
        Assert.Equal("workspace-write", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.False(parsed.ExecutionOptions.AllowNetwork);
        Assert.True(parsed.ExecutionOptions.RequiresApproval);
    }

    [Theory]
    [InlineData("status", (int)RoadmapCliCommand.Status)]
    [InlineData("run", (int)RoadmapCliCommand.Run)]
    [InlineData("unblock", (int)RoadmapCliCommand.Unblock)]
    public void TryParse_accepts_leading_command(string command, int expectedValue)
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([command, repo.Root], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal((RoadmapCliCommand)expectedValue, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_accepts_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([repo.Root, "unblock"], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.Unblock, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_rejects_unsupported_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.False(CliArguments.TryParse([repo.Root, "repair"], out RoadmapCliInvocation _, out string error));

        Assert.Contains("Unsupported roadmap command", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_elevated_execution_reason_after_leading_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            ["run", repo.Root, "--elevated", "Needs package registry"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("danger-full-access", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.True(parsed.ExecutionOptions.AllowNetwork);
        Assert.Equal("Needs package registry", parsed.ExecutionOptions.ElevatedReason);
    }

    [Fact]
    public void TryParse_accepts_elevated_execution_reason_after_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            [repo.Root, "run", "--elevated", "Needs package registry"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("danger-full-access", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.True(parsed.ExecutionOptions.AllowNetwork);
    }

    [Fact]
    public void TryParse_rejects_elevated_execution_without_reason()
    {
        using var repo = new TempRepo();

        Assert.False(CliArguments.TryParse([repo.Root, "--elevated"], out RoadmapCliInvocation _, out string error));

        Assert.Contains("requires a non-empty reason", error, StringComparison.Ordinal);
    }
}

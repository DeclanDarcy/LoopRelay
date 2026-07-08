using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryParse_requires_repo_dir()
    {
        Assert.False(Cli.CliArguments.TryParse([], out Cli.RoadmapCliInvocation _, out string error));
        Assert.Contains("REPO_DIR", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_existing_repo_dir()
    {
        using var repo = new TempRepo();

        Assert.True(Cli.CliArguments.TryParse([repo.Root], out Cli.RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(Cli.RoadmapCliCommand.Run, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
        Assert.Equal("workspace-write", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.False(parsed.ExecutionOptions.AllowNetwork);
        Assert.True(parsed.ExecutionOptions.RequiresApproval);
    }

    [Theory]
    [InlineData("status", (int)Cli.RoadmapCliCommand.Status)]
    [InlineData("run", (int)Cli.RoadmapCliCommand.Run)]
    [InlineData("unblock", (int)Cli.RoadmapCliCommand.Unblock)]
    [InlineData("semantic", (int)Cli.RoadmapCliCommand.Semantic)]
    public void TryParse_accepts_leading_command(string command, int expectedValue)
    {
        using var repo = new TempRepo();

        Assert.True(Cli.CliArguments.TryParse([command, repo.Root], out Cli.RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal((Cli.RoadmapCliCommand)expectedValue, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_accepts_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.True(Cli.CliArguments.TryParse([repo.Root, "unblock"], out Cli.RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(Cli.RoadmapCliCommand.Unblock, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_accepts_semantic_roadmap_transition_status_command()
    {
        using var repo = new TempRepo();

        Assert.True(Cli.CliArguments.TryParse(
            ["semantic", "roadmap-transition", "status", repo.Root],
            out Cli.RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(Cli.RoadmapCliCommand.SemanticRoadmapTransitionStatus, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_rejects_unsupported_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.False(Cli.CliArguments.TryParse([repo.Root, "repair"], out Cli.RoadmapCliInvocation _, out string error));

        Assert.Contains("Unsupported roadmap command", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_elevated_execution_reason_after_leading_command()
    {
        using var repo = new TempRepo();

        Assert.True(Cli.CliArguments.TryParse(
            ["run", repo.Root, "--elevated", "Needs package registry"],
            out Cli.RoadmapCliInvocation parsed,
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

        Assert.True(Cli.CliArguments.TryParse(
            [repo.Root, "run", "--elevated", "Needs package registry"],
            out Cli.RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("danger-full-access", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.True(parsed.ExecutionOptions.AllowNetwork);
    }

    [Fact]
    public void TryParse_rejects_elevated_execution_without_reason()
    {
        using var repo = new TempRepo();

        Assert.False(Cli.CliArguments.TryParse([repo.Root, "--elevated"], out Cli.RoadmapCliInvocation _, out string error));

        Assert.Contains("requires a non-empty reason", error, StringComparison.Ordinal);
    }
}

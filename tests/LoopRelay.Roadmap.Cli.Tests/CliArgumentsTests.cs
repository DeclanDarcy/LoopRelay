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
    }

    [Theory]
    [InlineData("status", (int)Cli.RoadmapCliCommand.Status)]
    [InlineData("run", (int)Cli.RoadmapCliCommand.Run)]
    [InlineData("unblock", (int)Cli.RoadmapCliCommand.Unblock)]
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
    public void TryParse_rejects_unsupported_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.False(Cli.CliArguments.TryParse([repo.Root, "repair"], out Cli.RoadmapCliInvocation _, out string error));

        Assert.Contains("Unsupported roadmap command", error, StringComparison.Ordinal);
    }
}

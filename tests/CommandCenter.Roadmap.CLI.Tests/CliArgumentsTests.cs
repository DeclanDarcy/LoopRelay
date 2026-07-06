using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

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
}

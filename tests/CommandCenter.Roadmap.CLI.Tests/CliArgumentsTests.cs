using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryParse_requires_repo_dir()
    {
        Assert.False(CliArguments.TryParse([], out _, out string error));
        Assert.Contains("REPO_DIR", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_existing_repo_dir()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([repo.Root], out var parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Path);
    }
}

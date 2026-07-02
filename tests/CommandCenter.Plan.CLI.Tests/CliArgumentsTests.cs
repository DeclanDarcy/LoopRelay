using CommandCenter.Plan.Cli;
using Xunit;

namespace CommandCenter.Plan.Cli.Tests;

public class CliArgumentsTests
{
    [Fact]
    public void TryParse_WithExistingDirectory_ReturnsRepositoryWithAbsolutePath()
    {
        string dir = Directory.CreateTempSubdirectory("cc-plan-cli-args").FullName;

        bool ok = CliArguments.TryParse(new[] { dir }, out var repository, out string error);

        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(dir), repository.Path);
        Assert.Equal(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), repository.Name);
        Assert.NotEqual(Guid.Empty, repository.Id);
    }

    [Fact]
    public void TryParse_WithNoArgs_Fails()
    {
        bool ok = CliArguments.TryParse(Array.Empty<string>(), out _, out string error);

        Assert.False(ok);
        Assert.Contains("REPO_DIR", error);
    }

    [Fact]
    public void TryParse_WithMissingDirectory_Fails()
    {
        bool ok = CliArguments.TryParse(new[] { "Z:/does/not/exist/cc-plan-cli" }, out _, out string error);

        Assert.False(ok);
        Assert.Contains("does not exist", error);
    }
}

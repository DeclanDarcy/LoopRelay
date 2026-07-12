using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Services.Process;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

// M3 clean-input gates require every declared input surface to resolve to a commit: a
// workspace without a git working tree stops with UnversionedInputSurface, and a dirty
// surface stops with DirtyInputSurface. Integration tests that exercise disk-reading
// transitions therefore run inside a real git repository with their collaboration
// inputs committed before the consuming transition runs.
internal static class GitWorkspace
{
    public static async Task InitializeAsync(string repositoryPath)
    {
        await GitAsync(repositoryPath, "init");
        await GitAsync(repositoryPath, "config", "user.name", "LoopRelay Tests");
        await GitAsync(repositoryPath, "config", "user.email", "tests@looprelay.local");
        await GitAsync(repositoryPath, "config", "commit.gpgsign", "false");
    }

    public static async Task CommitAgentsInputsAsync(string repositoryPath)
    {
        await GitAsync(repositoryPath, "add", "--", ".agents");
        await GitAsync(repositoryPath, "commit", "--allow-empty", "-m", "Commit collaboration inputs");
    }

    public static async Task InitializeWithAgentsInputsAsync(string repositoryPath)
    {
        await InitializeAsync(repositoryPath);
        await CommitAgentsInputsAsync(repositoryPath);
    }

    private static async Task GitAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {result.StandardError}");
    }
}

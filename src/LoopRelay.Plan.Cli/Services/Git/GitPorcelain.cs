namespace LoopRelay.Plan.Cli.Services.Git;

internal static class GitPorcelain
{
    public static IReadOnlyList<string> ChangedPaths(string statusOutput) =>
        Infrastructure.Services.Git.GitPorcelain.ChangedPaths(statusOutput);
}

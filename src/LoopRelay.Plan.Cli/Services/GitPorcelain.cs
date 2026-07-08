namespace LoopRelay.Plan.Cli;

internal static class GitPorcelain
{
    public static IReadOnlyList<string> ChangedPaths(string statusOutput) =>
        Infrastructure.Git.GitPorcelain.ChangedPaths(statusOutput);
}

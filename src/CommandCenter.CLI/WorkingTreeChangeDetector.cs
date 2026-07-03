using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// The ONE definition of "did this iteration change anything real": the target repository's
/// <c>git status --porcelain</c> paths with the <c>.agents</c> submodule filtered out. The parent working
/// tree only ever surfaces the submodule as a single <c>.agents</c> gitlink entry (a moved pointer from the
/// pre-codex publish by <see cref="AgentsSubmodulePublisher"/>), so it is never progress. Two consumers hang
/// real behavior off this answer — <see cref="CommitGate"/> decides commit-vs-stall and
/// <see cref="ExecutionStep"/> picks the handoff prompt — so the rule lives here and only here; if they ever
/// disagreed, an iteration could stall as "no progress" while its handoff claimed work happened (or vice versa).
///
/// Git is driven through <see cref="IProcessRunner"/> with <c>workingDirectory = repository.Path</c> (no
/// <c>-C</c>), mirroring <c>GitService</c>; a nonzero exit throws <see cref="LoopStepException"/>, which the
/// loop surfaces as a failed run.
/// </summary>
internal sealed class WorkingTreeChangeDetector(IProcessRunner processRunner, Repository repository)
{
    public async Task<IReadOnlyList<string>> GetRealChangedPathsAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["status", "--porcelain"], repository.Path);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git status failed: {result.StandardError}");
        }

        return GitPorcelain.ChangedPaths(result.StandardOutput)
            .Where(path =>
                path != OrchestrationArtifactPaths.AgentsDirectory &&
                !path.StartsWith(OrchestrationArtifactPaths.AgentsDirectory + "/", StringComparison.Ordinal))
            .ToList();
    }
}

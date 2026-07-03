using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// Commits and pushes the target repository's real working-tree changes at the end of each loop iteration,
/// then applies the no-substantive-change stall gate.
///
/// The <c>.agents/</c> submodule is deliberately IGNORED here: it is committed and pushed to its own remote by
/// <see cref="AgentsSubmodulePublisher"/> before each codex turn, so this gate neither stages nor advances the
/// <c>.agents</c> gitlink pointer, and it never counts a lone submodule change as progress. Consequently
/// "substantive progress" means a real (non-<c>.agents</c>) repository file changed — the shared
/// <see cref="WorkingTreeChangeDetector"/> rule, which the handoff-prompt choice also keys on — so an
/// iteration that touched only <c>.agents/</c> made no progress and increments <see cref="NoChangesCount"/>,
/// while any real change resets it to 0. Once the count exceeds <see cref="MaxNoChangesCount"/> the loop is
/// asked to stop.
///
/// Git is driven through <see cref="IProcessRunner"/> with <c>workingDirectory = repository.Path</c> (no
/// <c>-C</c>), mirroring <c>GitService</c>; a nonzero exit on any git call aborts the iteration by throwing
/// <see cref="LoopStepException"/>, which the loop surfaces as a failed run.
/// </summary>
internal sealed class CommitGate(
    WorkingTreeChangeDetector changeDetector, IProcessRunner processRunner, Repository repository, ILoopConsole console)
{
    internal const int MaxNoChangesCount = 2;

    private const string CommitMessage = "Orchestration loop: automated execution and decision iteration";

    // Stage everything EXCEPT the `.agents` submodule, so the loop never commits or advances the gitlink from
    // here (the submodule is published independently to its own remote by AgentsSubmodulePublisher).
    private static readonly string[] AddExcludingAgents =
        ["add", "-A", "--", ".", ":(exclude)" + OrchestrationArtifactPaths.AgentsDirectory];

    private int noChangesCount;

    internal int NoChangesCount => noChangesCount;

    /// <summary>
    /// Commits and pushes the target repository's real (non-<c>.agents</c>) working-tree changes, then evaluates
    /// the stall gate. Returns true if the loop should STOP (NoChangesCount has exceeded MaxNoChangesCount).
    /// </summary>
    public async Task<bool> CommitPushAndEvaluateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> changed = await changeDetector.GetRealChangedPathsAsync();

        if (changed.Count > 0)
        {
            await RunGitAsync("add", AddExcludingAgents);
            await RunGitAsync("commit", ["commit", "-m", CommitMessage]);
            await RunGitAsync("push", ["push"]);
            noChangesCount = 0;
        }
        else
        {
            noChangesCount++;
            console.Info($"No substantive changes this iteration ({noChangesCount}/{MaxNoChangesCount}).");
        }

        if (noChangesCount > MaxNoChangesCount)
        {
            console.Warn(
                $"No substantive changes across {noChangesCount} consecutive iterations — stalling the loop.");
            return true;
        }

        return false;
    }

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, repository.Path);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git {label} failed: {result.StandardError}");
        }
    }
}

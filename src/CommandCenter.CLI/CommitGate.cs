using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// Commits and pushes the target repository's working tree at the end of each loop iteration, then
/// applies the no-substantive-change stall gate. An iteration that changed ONLY bookkeeping files
/// (everything under <c>.agents/decisions/</c> and <c>.agents/handoffs/</c>) makes no real progress, so
/// it increments <see cref="NoChangesCount"/>; any iteration that touched something else resets it to 0.
/// Once the count exceeds <see cref="MaxNoChangesCount"/> the loop is asked to stop (the run is stalled).
///
/// Git is driven through <see cref="IProcessRunner"/> with <c>workingDirectory = repository.Path</c> (no
/// <c>-C</c>), mirroring <c>GitService</c>; a nonzero exit on any git call aborts the iteration by throwing
/// <see cref="LoopStepException"/>, which the loop surfaces as a failed run.
/// </summary>
internal sealed class CommitGate(IProcessRunner processRunner, Repository repository, ILoopConsole console)
{
    internal const int MaxNoChangesCount = 2;

    private const string CommitMessage = "Orchestration loop: automated execution and decision iteration";

    // Bookkeeping prefixes derived from the canonical artifact-path constants (never hardcoded).
    private static readonly string DecisionsPrefix = OrchestrationArtifactPaths.DecisionsDirectory + "/";
    private static readonly string HandoffsPrefix = OrchestrationArtifactPaths.HandoffsDirectory + "/";

    private int noChangesCount;

    internal int NoChangesCount => noChangesCount;

    /// <summary>
    /// Commits and pushes the target repository's working tree, then evaluates the no-substantive-change
    /// stall gate. Returns true if the loop should STOP (NoChangesCount has exceeded MaxNoChangesCount).
    /// </summary>
    public async Task<bool> CommitPushAndEvaluateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> changed = await GetChangedPathsAsync();
        bool onlyBookkeeping = changed.All(IsBookkeeping);

        if (changed.Count > 0)
        {
            await RunGitAsync("add", ["add", "-A"]);
            await RunGitAsync("commit", ["commit", "-m", CommitMessage]);
            await RunGitAsync("push", ["push"]);
        }

        if (onlyBookkeeping)
        {
            noChangesCount++;
            console.Info($"No substantive changes this iteration ({noChangesCount}/{MaxNoChangesCount}).");
        }
        else
        {
            noChangesCount = 0;
        }

        if (noChangesCount > MaxNoChangesCount)
        {
            console.Warn(
                $"No substantive changes across {noChangesCount} consecutive iterations — stalling the loop.");
            return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> GetChangedPathsAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["status", "--porcelain"], repository.Path);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git status failed: {result.StandardError}");
        }

        var changed = new List<string>();
        foreach (string rawLine in result.StandardOutput.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 4)
            {
                continue;
            }

            // Drop the 2-char XY status + the separating space; keep the path.
            string path = line[3..];

            // A rename/copy entry is "old -> new"; the new path is the one that now exists.
            int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                path = path[(arrow + " -> ".Length)..];
            }

            changed.Add(path.Replace('\\', '/').Trim('"'));
        }

        return changed;
    }

    private static bool IsBookkeeping(string path) =>
        path.StartsWith(DecisionsPrefix, StringComparison.Ordinal) ||
        path.StartsWith(HandoffsPrefix, StringComparison.Ordinal);

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, repository.Path);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git {label} failed: {result.StandardError}");
        }
    }
}

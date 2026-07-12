using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

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
/// ONE <c>.agents/</c> change does count: a reduction in unchecked milestone items across the iteration
/// (fewer open boxes after execution than before it) is treated exactly like a real code change and resets
/// the counter. Milestone box-ticks are how an epic advances when the repository itself needn't change —
/// verification/audit milestones, or work the agent already committed itself — and without this credit the
/// stall gate would kill a loop that is in fact converging on epic completion.
///
/// Git is driven through <see cref="IProcessRunner"/> with <c>workingDirectory = repository.Path</c> (no
/// <c>-C</c>), mirroring <c>GitService</c>; a nonzero exit on any git call aborts the iteration by throwing
/// <see cref="LoopStepException"/>, which the loop surfaces as a failed run.
/// </summary>
internal sealed class CommitGate(
    WorkingTreeChangeDetector _changeDetector, IProcessRunner _processRunner, Repository _repository, ILoopConsole _console,
    int _maxNoChangesCount = OperationalPolicyResolver.DefaultMaxNoChangesCommits)
{
    internal int MaxNoChangesCount => _maxNoChangesCount;

    private const string CommitMessage = "Orchestration loop: automated execution and decision iteration";
    private const string NonImplementationReviewDecisionMessage =
        "Orchestration loop: apply non-implementation review decisions";

    // Stage everything EXCEPT the `.agents` submodule, so the loop never commits or advances the gitlink from
    // here (the submodule is published independently to its own remote by AgentsSubmodulePublisher).
    private static readonly string[] AddExcludingAgents =
        ["add", "-A", "--", ".", ":(exclude)" + OrchestrationArtifactPaths.AgentsDirectory];

    private int noChangesCount;

    internal int NoChangesCount => noChangesCount;

    /// <summary>
    /// Commits and pushes the target repository's real (non-<c>.agents</c>) working-tree changes, then evaluates
    /// the stall gate. The caller supplies the unchecked-milestone-item counts from before and after the
    /// execution turn; a reduction counts as substantive progress even with a clean working tree. Returns true
    /// if the loop should STOP (NoChangesCount has exceeded MaxNoChangesCount).
    /// </summary>
    public async Task<bool> CommitPushAndEvaluateAsync(
        int uncheckedMilestonesBefore, int uncheckedMilestonesAfter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> changed = await _changeDetector.GetRealChangedPathsAsync();

        if (changed.Count > 0)
        {
            await RunGitAsync("add", AddExcludingAgents);
            await RunGitAsync("commit", ["commit", "-m", CommitMessage]);
            await RunGitAsync("push", ["push"]);
            noChangesCount = 0;
        }
        else if (uncheckedMilestonesAfter < uncheckedMilestonesBefore)
        {
            // Milestone box-ticks live inside `.agents/` (ignored here; published to its own remote by
            // AgentsSubmodulePublisher), so there is nothing to commit — but fewer open milestone items than
            // the iteration started with is real progress toward the epic, so it resets the stall counter
            // exactly like a code change. Only a REDUCTION counts: an execution that merely rewrote or added
            // milestone items still falls through to the no-progress branch.
            noChangesCount = 0;
            _console.Info(
                $"No repository changes, but unchecked milestone items fell from {uncheckedMilestonesBefore} " +
                $"to {uncheckedMilestonesAfter} — counting as substantive progress.");
        }
        else
        {
            noChangesCount++;
            _console.Info($"No substantive changes this iteration ({noChangesCount}/{_maxNoChangesCount}).");
        }

        if (noChangesCount > _maxNoChangesCount)
        {
            _console.Warn(
                $"No substantive changes across {noChangesCount} consecutive iterations — stalling the loop.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Commits and pushes parent-repository changes produced by approved completion-review actions without
    /// changing the iteration stall counter.
    /// </summary>
    public async Task<bool> CommitPushIfChangedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> changed = await _changeDetector.GetRealChangedPathsAsync();
        if (changed.Count == 0)
        {
            return false;
        }

        await RunGitAsync("add", AddExcludingAgents);
        await RunGitAsync("commit", ["commit", "-m", NonImplementationReviewDecisionMessage]);
        await RunGitAsync("push", ["push"]);
        _console.Info("Committed and pushed approved non-implementation review repository changes.");
        return true;
    }

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await _processRunner.RunAsync("git", arguments, _repository.Path);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git {label} failed: {result.StandardError}");
        }
    }
}

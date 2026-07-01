using System.IO;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// Commits and pushes the target repository's <c>.agents/</c> git submodule to its own remote. Because
/// <c>.agents/</c> is a submodule, the parent repository cannot commit the content inside it, so this
/// publisher owns that commit — it is the ONLY thing in the CLI loop that versions the submodule
/// (<see cref="CommitGate"/> deliberately ignores <c>.agents/</c>). <see cref="LoopRunner"/> runs it twice
/// per iteration: once BEFORE invoking codex (persisting the operational context / decisions / rotated
/// handoff codex is about to consume) and once AFTER (capturing codex's writes — the new handoff and any
/// milestone box-checks — so even the epic-completing/stalling iteration's state reaches the remote).
///
/// Git is driven through <see cref="IProcessRunner"/> with
/// <c>workingDirectory = {repository.Path}/.agents</c>; any nonzero git exit throws
/// <see cref="LoopStepException"/> (strict push, mirroring <see cref="CommitGate"/>).
/// </summary>
internal sealed class AgentsSubmodulePublisher(IProcessRunner processRunner, Repository repository, ILoopConsole console)
{
    /// <summary>Commit message for the pre-codex publish — the context codex will consume.</summary>
    public const string ContextUpdateMessage = "Orchestration loop: context update before execution";

    /// <summary>Commit message for the post-codex publish — codex's handoff and milestone writes.</summary>
    public const string ExecutionHandoffMessage = "Orchestration loop: execution handoff";

    /// <summary>Commit message for the best-effort salvage publish when the loop exits abnormally (Failed/Cancelled).</summary>
    public const string PartialExitMessage = "Orchestration loop: partial state on interrupted exit";

    private string SubmodulePath => Path.Combine(repository.Path, OrchestrationArtifactPaths.AgentsDirectory);

    /// <summary>
    /// Commits and pushes the <c>.agents/</c> submodule when it has changes. Returns true if a commit was
    /// made, false if the submodule working tree was already clean (never creates an empty commit). When the
    /// tree is clean it still pushes any commit stranded by a prior failed push (see below), so a transient
    /// push failure self-heals on the next publish instead of leaving the parent gitlink referencing a
    /// commit that is absent from the remote.
    /// </summary>
    public async Task<bool> PublishAsync(string commitMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await HasChangesAsync())
        {
            string branch = await RequireBranchAsync();
            await RunGitAsync("add", ["add", "-A"]);
            await RunGitAsync("commit", ["commit", "-m", commitMessage]);
            await RunGitAsync("push", ["push"]);
            console.Info($"Committed and pushed the .agents submodule ({branch}).");
            return true;
        }

        // Working tree is clean. A failed `git push` does NOT roll back the commit, so a prior strict-push
        // failure can leave a committed-but-unpushed submodule that `git status` can no longer see. Recover
        // it here by pushing when HEAD is ahead of its upstream, so the remote (and any parent gitlink that
        // references it) eventually catches up.
        if (await HasUnpushedCommitsAsync())
        {
            string branch = await RequireBranchAsync();
            await RunGitAsync("push", ["push"]);
            console.Info($"Pushed a previously-stranded .agents submodule commit ({branch}).");
        }

        return false;
    }

    // A submodule checked out at a detached HEAD cannot be pushed — a commit would land on no branch — so
    // fail fast with an actionable message rather than committing/pushing to a dead ref. (In this repo the
    // submodule tracks a branch; other repos may have been initialised detached.)
    private async Task<string> RequireBranchAsync()
    {
        string branch = await CurrentBranchAsync();
        if (string.IsNullOrEmpty(branch))
        {
            throw new LoopStepException(
                $"The .agents submodule at '{SubmodulePath}' is in detached HEAD; check out its tracking " +
                "branch so the loop can commit and push it.");
        }

        return branch;
    }

    // True when the submodule HEAD is ahead of its upstream (unpushed commits). A non-zero exit (e.g. no
    // upstream configured) is treated as "nothing recoverable" rather than an error.
    private async Task<bool> HasUnpushedCommitsAsync()
    {
        ProcessRunResult result =
            await processRunner.RunAsync("git", ["rev-list", "--count", "@{u}..HEAD"], SubmodulePath);
        if (result.ExitCode != 0)
        {
            return false;
        }

        return int.TryParse(result.StandardOutput.Trim(), out int count) && count > 0;
    }

    private async Task<bool> HasChangesAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["status", "--porcelain"], SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git status (.agents submodule) failed: {result.StandardError}");
        }

        return GitPorcelain.ChangedPaths(result.StandardOutput).Count > 0;
    }

    private async Task<string> CurrentBranchAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["branch", "--show-current"], SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException(
                $"git branch --show-current (.agents submodule) failed: {result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new LoopStepException($"git {label} (.agents submodule) failed: {result.StandardError}");
        }
    }
}

using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Plan.Cli;

/// <summary>
/// Commits and pushes the target repository's <c>.agents/</c> git submodule to its own remote. Ported from the
/// reference CLI (internals cannot be referenced across projects — accepted duplication), with the parent-gitlink
/// reconcile SPLIT out of the publish so the pipeline controls its cadence: the submodule is committed+pushed
/// after every artifact-mutating pipeline step (<see cref="PublishAgentsAsync"/>), while the parent repo's moved
/// <c>.agents</c> gitlink pointer is recorded ONCE at the end of a successful run
/// (<see cref="RecordParentGitlinkAsync"/>) — except for the epic-archive step, which records it immediately:
/// that run may still stop at preflight (e.g. no specs/epic.md ever existed), and the archive must be
/// fully published, parent pointer included, either way.
///
/// Git is driven through <see cref="IProcessRunner"/>: the submodule half runs with
/// <c>workingDirectory = {repository.Path}/.agents</c>, the gitlink half with
/// <c>workingDirectory = {repository.Path}</c>. Any nonzero git exit throws <see cref="PlanStepException"/>
/// (strict push).
/// </summary>
internal sealed class AgentsSubmodulePublisher(IProcessRunner processRunner, Repository repository, ILoopConsole console)
{
    /// <summary>Commit message for the pre-preflight epic-archive publish (the new-epic rollover's writes).</summary>
    public const string ArchivePreviousEpicMessage = "Plan pipeline: archive previous epic";

    /// <summary>Commit message for the publish after the Write Plan step.</summary>
    public const string WritePlanMessage = "Plan pipeline: write plan";

    /// <summary>Commit message for the publish after the Revise Plan step (covers both the revised plan.md and
    /// the operational_context.md seeded from it — one commit for the pair).</summary>
    public const string RevisePlanMessage = "Plan pipeline: revise plan and seed operational context";

    /// <summary>Commit message for the publish after the Collect Details step.</summary>
    public const string CollectDetailsMessage = "Plan pipeline: collect details";

    /// <summary>Commit message for the publish after the Extract Milestones step.</summary>
    public const string ExtractMilestonesMessage = "Plan pipeline: extract milestones";

    /// <summary>Commit message for the publish after the Extract Details step.</summary>
    public const string ExtractDetailsMessage = "Plan pipeline: extract details";

    /// <summary>
    /// Commit message for the PARENT-repo commit that records the moved <c>.agents</c> gitlink. Advancing the
    /// submodule HEAD leaves the parent working tree showing a dirty <c>.agents</c> entry; this commit versions
    /// that pointer so the parent tree ends the run clean.
    /// </summary>
    public const string GitlinkPointerMessage = "Plan pipeline: record .agents submodule pointer";

    private string SubmodulePath => Path.Combine(repository.Path, OrchestrationArtifactPaths.AgentsDirectory);

    /// <summary>
    /// Commits and pushes the <c>.agents/</c> submodule when it has changes. Returns true if a commit was
    /// made, false if the submodule working tree was already clean (never creates an empty commit). When the
    /// tree is clean it still pushes any commit stranded by a prior failed push (see below), so a transient
    /// push failure self-heals on the next publish instead of leaving the parent gitlink referencing a
    /// commit that is absent from the remote. Never touches the parent repo — the caller decides when the
    /// moved gitlink pointer is recorded via <see cref="RecordParentGitlinkAsync"/>.
    /// </summary>
    public async Task<bool> PublishAgentsAsync(string commitMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool committed = false;
        if (await HasChangesAsync())
        {
            string branch = await RequireBranchAsync();
            await RunGitAsync("add", ["add", "-A"]);
            await RunGitAsync("commit", ["commit", "-m", commitMessage]);
            await PushSubmoduleAsync();
            console.Info($"Committed and pushed the .agents submodule ({branch}).");
            committed = true;
        }
        // Working tree is clean. A failed `git push` does NOT roll back the commit, so a prior strict-push
        // failure can leave a committed-but-unpushed submodule that `git status` can no longer see. Recover
        // it here by pushing when HEAD is ahead of its upstream, so the remote (and any parent gitlink that
        // references it) eventually catches up.
        else if (await HasUnpushedCommitsAsync())
        {
            string branch = await RequireBranchAsync();
            await PushSubmoduleAsync();
            console.Info($"Pushed a previously-stranded .agents submodule commit ({branch}).");
        }

        return committed;
    }

    /// <summary>
    /// Commits and pushes the parent repo's advanced <c>.agents</c> gitlink. Runs git in the PARENT working
    /// tree (repository.Path), staging ONLY <c>.agents</c> so the parent's real working-tree changes stay
    /// untouched; strict push, matching the submodule's posture. The caller gates this on a fresh submodule
    /// commit having been made — a new commit necessarily moves the pointer, so no status probe is needed.
    /// </summary>
    public async Task RecordParentGitlinkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RunParentGitAsync("add", ["add", "--", OrchestrationArtifactPaths.AgentsDirectory]);
        await RunParentGitAsync("commit", ["commit", "-m", GitlinkPointerMessage]);
        await PushParentGitlinkAsync();
        console.Info("Recorded and pushed the .agents submodule pointer in the parent repo.");
    }

    private async Task PushParentGitlinkAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["push"], repository.Path);
        if (result.ExitCode == 0)
        {
            return;
        }

        if (await ParentUpstreamAlreadyAtHeadAsync())
        {
            console.Info("The parent .agents submodule pointer was already present on the upstream branch.");
            return;
        }

        throw new PlanStepException(
            $"git push (.agents gitlink, parent repo) failed: {result.StandardError}");
    }

    private async Task<bool> ParentUpstreamAlreadyAtHeadAsync()
    {
        ProcessRunResult fetch = await processRunner.RunAsync("git", ["fetch", "--quiet"], repository.Path);
        if (fetch.ExitCode != 0)
        {
            return false;
        }

        ProcessRunResult head = await processRunner.RunAsync("git", ["rev-parse", "HEAD"], repository.Path);
        if (head.ExitCode != 0)
        {
            return false;
        }

        ProcessRunResult upstream = await processRunner.RunAsync("git", ["rev-parse", "@{u}"], repository.Path);
        if (upstream.ExitCode != 0)
        {
            return false;
        }

        string headSha = head.StandardOutput.Trim();
        string upstreamSha = upstream.StandardOutput.Trim();
        return headSha.Length > 0 &&
            string.Equals(headSha, upstreamSha, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunParentGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, repository.Path);
        if (result.ExitCode != 0)
        {
            throw new PlanStepException(
                $"git {label} (.agents gitlink, parent repo) failed: {result.StandardError}");
        }
    }

    // A submodule checked out at a detached HEAD cannot be pushed — a commit would land on no branch — so
    // fail fast with an actionable message rather than committing/pushing to a dead ref. (In this repo the
    // submodule tracks a branch; other repos may have been initialised detached.)
    private async Task<string> RequireBranchAsync()
    {
        string branch = await CurrentBranchAsync();
        if (string.IsNullOrEmpty(branch))
        {
            throw new PlanStepException(
                $"The .agents submodule at '{SubmodulePath}' is in detached HEAD; check out its tracking " +
                "branch so the pipeline can commit and push it.");
        }

        return branch;
    }

    private async Task PushSubmoduleAsync()
    {
        ProcessRunResult first = await processRunner.RunAsync("git", ["push"], SubmodulePath);
        if (first.ExitCode == 0)
        {
            return;
        }

        if (await SubmoduleUpstreamAlreadyAtHeadAsync())
        {
            console.Info("The .agents submodule commit was already present on the upstream branch.");
            return;
        }

        ProcessRunResult retry = await processRunner.RunAsync("git", ["push"], SubmodulePath);
        if (retry.ExitCode == 0)
        {
            console.Info("Retried the .agents submodule push after a transient failure.");
            return;
        }

        if (await SubmoduleUpstreamAlreadyAtHeadAsync())
        {
            console.Info("The .agents submodule commit reached the upstream branch after a push retry.");
            return;
        }

        throw new PlanStepException(
            $"git push (.agents submodule) failed: {NonEmpty(retry.StandardError, first.StandardError)}");
    }

    private async Task<bool> SubmoduleUpstreamAlreadyAtHeadAsync()
    {
        ProcessRunResult fetch = await processRunner.RunAsync("git", ["fetch", "--quiet"], SubmodulePath);
        if (fetch.ExitCode != 0)
        {
            return false;
        }

        ProcessRunResult head = await processRunner.RunAsync("git", ["rev-parse", "HEAD"], SubmodulePath);
        if (head.ExitCode != 0)
        {
            return false;
        }

        ProcessRunResult upstream = await processRunner.RunAsync("git", ["rev-parse", "@{u}"], SubmodulePath);
        if (upstream.ExitCode != 0)
        {
            return false;
        }

        string headSha = head.StandardOutput.Trim();
        string upstreamSha = upstream.StandardOutput.Trim();
        return headSha.Length > 0 &&
            string.Equals(headSha, upstreamSha, StringComparison.OrdinalIgnoreCase);
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
            throw new PlanStepException($"git status (.agents submodule) failed: {result.StandardError}");
        }

        return GitPorcelain.ChangedPaths(result.StandardOutput).Count > 0;
    }

    private async Task<string> CurrentBranchAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["branch", "--show-current"], SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new PlanStepException(
                $"git branch --show-current (.agents submodule) failed: {result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new PlanStepException($"git {label} (.agents submodule) failed: {result.StandardError}");
        }
    }

    private static string NonEmpty(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}

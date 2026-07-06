using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Orchestration;

namespace LoopRelay.Cli;

/// <summary>
/// Commits and pushes the target repository's <c>.agents/</c> git submodule to its own remote, then records the
/// moved gitlink in the parent repository. Because <c>.agents/</c> is a submodule, the parent repository cannot
/// commit the content inside it, so this publisher owns that commit — it is the ONLY thing in the CLI loop that
/// versions the submodule (<see cref="CommitGate"/> deliberately ignores <c>.agents/</c>). <see cref="LoopRunner"/>
/// runs it twice per iteration: once BEFORE invoking codex (persisting the operational context / decisions /
/// rotated handoff codex is about to consume) and once AFTER (capturing codex's writes — the new handoff and any
/// milestone box-checks — so even the epic-completing/stalling iteration's state reaches the remote).
///
/// Committing a new submodule revision advances the submodule HEAD, which leaves the PARENT working tree showing
/// a dirty <c>.agents</c> gitlink entry; right after that commit this class also commits and pushes the moved
/// pointer in the parent repo (staging only <c>.agents</c>) so the working tree codex opens on is clean and
/// never distracts it with pending changes. No status probe gates this — a fresh submodule commit is itself the
/// proof the pointer moved.
///
/// Git is driven through <see cref="IProcessRunner"/>: the submodule half runs with
/// <c>workingDirectory = {repository.Path}/.agents</c>, the gitlink half with
/// <c>workingDirectory = {repository.Path}</c>. Any nonzero git exit throws <see cref="LoopStepException"/>
/// (strict push, mirroring <see cref="CommitGate"/>).
/// </summary>
internal sealed class AgentsSubmodulePublisher(IProcessRunner processRunner, Repository repository, ILoopConsole console)
{
    /// <summary>Commit message for the pre-codex publish — the context codex will consume.</summary>
    public const string ContextUpdateMessage = "Orchestration loop: context update before execution";

    /// <summary>Commit message for the post-codex publish — codex's handoff and milestone writes.</summary>
    public const string ExecutionHandoffMessage = "Orchestration loop: execution handoff";

    /// <summary>Commit message for the best-effort salvage publish when the loop exits abnormally (Failed/Cancelled).</summary>
    public const string PartialExitMessage = "Orchestration loop: partial state on interrupted exit";

    /// <summary>
    /// Commit message for the PARENT-repo commit that records the moved <c>.agents</c> gitlink. Advancing the
    /// submodule HEAD leaves the parent working tree showing a dirty <c>.agents</c> entry; this commit versions
    /// that pointer so the tree codex opens on is clean (and it never distracts codex with pending changes).
    /// </summary>
    public const string GitlinkPointerMessage = "Orchestration loop: record .agents submodule pointer";

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

        // A fresh submodule commit just advanced the submodule HEAD, so the parent repo's `.agents` gitlink now
        // lags it and shows dirty in the parent working tree. Record and push that pointer so the tree codex
        // opens on is clean. No `git status` probe is needed to decide this: a new commit necessarily moves the
        // pointer (and a submodule push only fails BEFORE this point, so `committed` is exactly the "pointer
        // moved and was not yet recorded" signal). CommitGate deliberately never touches `.agents`, so this is
        // the ONLY place the parent gitlink is versioned. A stranded-commit recovery above skips this; its
        // pointer catches up on the next real commit's reconcile.
        if (committed)
        {
            await RecordParentGitlinkAsync();
        }

        return committed;
    }

    // Commits and pushes the parent repo's advanced `.agents` gitlink. Runs git in the PARENT working tree
    // (repository.Path), staging ONLY `.agents` so the parent's real working-tree changes stay owned by
    // CommitGate; strict push, matching the submodule's posture.
    private async Task RecordParentGitlinkAsync()
    {
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

        throw new LoopStepException(
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
            throw new LoopStepException(
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
            throw new LoopStepException(
                $"The .agents submodule at '{SubmodulePath}' is in detached HEAD; check out its tracking " +
                "branch so the loop can commit and push it.");
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

        throw new LoopStepException(
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

    private static string NonEmpty(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}

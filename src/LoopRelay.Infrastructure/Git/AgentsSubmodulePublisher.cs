using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Git;

public sealed record AgentsSubmodulePublisherOptions(
    string AgentsDirectory = ".agents",
    string ActorName = "workflow");

public sealed class AgentsSubmodulePublisherException : Exception
{
    public AgentsSubmodulePublisherException(string message) : base(message)
    {
    }
}

/// <summary>Commits, pushes, and records a repository's <c>.agents</c> submodule gitlink.</summary>
public sealed class AgentsSubmodulePublisher(
    IProcessRunner processRunner,
    Repository repository,
    ILoopConsole console,
    AgentsSubmodulePublisherOptions? options = null)
{
    private readonly AgentsSubmodulePublisherOptions options = options ?? new AgentsSubmodulePublisherOptions();

    private string SubmodulePath => Path.Combine(repository.Path, options.AgentsDirectory);

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
        else if (await HasUnpushedCommitsAsync())
        {
            string branch = await RequireBranchAsync();
            await PushSubmoduleAsync();
            console.Info($"Pushed a previously-stranded .agents submodule commit ({branch}).");
        }

        return committed;
    }

    public async Task RecordParentGitlinkAsync(string commitMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RunParentGitAsync("add", ["add", "--", options.AgentsDirectory]);
        await RunParentGitAsync("commit", ["commit", "-m", commitMessage]);
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

        throw new AgentsSubmodulePublisherException(
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
            throw new AgentsSubmodulePublisherException(
                $"git {label} (.agents gitlink, parent repo) failed: {result.StandardError}");
        }
    }

    private async Task<string> RequireBranchAsync()
    {
        string branch = await CurrentBranchAsync();
        if (string.IsNullOrEmpty(branch))
        {
            throw new AgentsSubmodulePublisherException(
                $"The .agents submodule at '{SubmodulePath}' is in detached HEAD; check out its tracking " +
                $"branch so the {options.ActorName} can commit and push it.");
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

        throw new AgentsSubmodulePublisherException(
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
            throw new AgentsSubmodulePublisherException($"git status (.agents submodule) failed: {result.StandardError}");
        }

        return GitPorcelain.ChangedPaths(result.StandardOutput).Count > 0;
    }

    private async Task<string> CurrentBranchAsync()
    {
        ProcessRunResult result = await processRunner.RunAsync("git", ["branch", "--show-current"], SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new AgentsSubmodulePublisherException(
                $"git branch --show-current (.agents submodule) failed: {result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private async Task RunGitAsync(string label, IReadOnlyList<string> arguments)
    {
        ProcessRunResult result = await processRunner.RunAsync("git", arguments, SubmodulePath);
        if (result.ExitCode != 0)
        {
            throw new AgentsSubmodulePublisherException($"git {label} (.agents submodule) failed: {result.StandardError}");
        }
    }

    private static string NonEmpty(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}

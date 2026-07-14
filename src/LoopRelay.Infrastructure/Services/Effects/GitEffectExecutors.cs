using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Services.Effects;

public abstract class GitEffectExecutorBase : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected GitEffectExecutorBase(Repository repository, IProcessRunner processRunner)
    {
        Repository = repository;
        ProcessRunner = processRunner;
    }

    protected Repository Repository { get; }
    protected IProcessRunner ProcessRunner { get; }
    public abstract EffectExecutorKey Key { get; }
    public string Version => "1";
    public abstract Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken);

    protected GitEffectPayload Payload(EffectIntent intent) =>
        JsonSerializer.Deserialize<GitEffectPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Git effect payload is invalid.");

    protected string WorkingDirectory(GitEffectPayload payload)
    {
        string root = Path.GetFullPath(Repository.Path);
        string path = Path.GetFullPath(Path.Combine(root,
            payload.RepositoryRelativeWorkingDirectory.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Git effect working directory escapes the repository root.");
        return path;
    }

    protected async Task<ProcessRunResult> GitAsync(string workingDirectory, string[] arguments) =>
        await ProcessRunner.RunAsync("git", arguments, workingDirectory);

    protected async Task<string> HeadAsync(string workingDirectory)
    {
        ProcessRunResult result = await GitAsync(workingDirectory, ["rev-parse", "HEAD"]);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : "unborn";
    }

    protected static EffectExecutionObservation Failure(ProcessRunResult result, string operation) => new(
        EffectLifecycle.Failed,
        $"{operation} failed with exit code {result.ExitCode}: " +
            string.Join(' ', new[] { result.StandardError, result.StandardOutput }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())),
        [$"exit:{result.ExitCode}"], "unknown", "unknown", false);
}

public sealed class NestedRepositoryCommitEffectExecutor(Repository repository, IProcessRunner processRunner)
    : GitEffectExecutorBase(repository, processRunner)
{
    public override EffectExecutorKey Key => GitEffectExecutorKeys.NestedRepositoryCommit;

    public override async Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        GitEffectPayload payload = Payload(intent);
        string directory = WorkingDirectory(payload);
        if (ProcessRunner is ProcessRunner)
        {
            string authority = Path.Combine(directory, ".git");
            if (!Directory.Exists(authority) && !File.Exists(authority))
                throw new InvalidOperationException("Nested publication target is not an independent Git repository.");
        }
        string before = await HeadAsync(directory);
        string[] statusArguments = payload.Pathspec is null
            ? ["status", "--porcelain"]
            : ["status", "--porcelain", "--", payload.Pathspec];
        ProcessRunResult status = await GitAsync(directory, statusArguments);
        if (status.ExitCode != 0) return Failure(status, "git status");
        if (string.IsNullOrWhiteSpace(status.StandardOutput))
            return new(EffectLifecycle.Succeeded, "Nested repository already clean.", [before], before, before, true, before);
        string[] addArguments = payload.Pathspec is null
            ? ["add", "-A"]
            : ["add", "-A", "--", payload.Pathspec];
        ProcessRunResult add = await GitAsync(directory, addArguments);
        if (add.ExitCode != 0) return Failure(add, "git add");
        ProcessRunResult commit = await GitAsync(directory, ["commit", "-m", payload.CommitMessage]);
        if (commit.ExitCode != 0) return Failure(commit, "git commit");
        string after = await HeadAsync(directory);
        return new(EffectLifecycle.Succeeded, "Nested repository commit created.", [after], before, after, after != "unborn", after);
    }
}

public sealed class GitPushEffectExecutor(
    Repository repository,
    IProcessRunner processRunner,
    EffectExecutorKey key) : GitEffectExecutorBase(repository, processRunner)
{
    public override EffectExecutorKey Key { get; } = key;

    public override async Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        GitEffectPayload payload = Payload(intent);
        string directory = WorkingDirectory(payload);
        string before = await HeadAsync(directory);
        ProcessRunResult push = await GitAsync(directory, ["push"]);
        if (push.ExitCode != 0)
            return new(EffectLifecycle.Pending, "Remote push is known incomplete.",
                [$"exit:{push.ExitCode}"], before, before, false);
        string upstream = await UpstreamAsync(directory);
        bool verified = before != "unborn" && string.Equals(before, upstream, StringComparison.OrdinalIgnoreCase);
        return new(verified ? EffectLifecycle.Succeeded : EffectLifecycle.Pending,
            verified ? "Remote ref matches local HEAD." : "Push returned success but upstream ref is not yet verified.",
            [before, upstream], before, upstream, verified, upstream);
    }

    private async Task<string> UpstreamAsync(string directory)
    {
        ProcessRunResult result = await GitAsync(directory, ["rev-parse", "@{u}"]);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : "missing-upstream";
    }
}

public sealed class ParentGitlinkCommitEffectExecutor(Repository repository, IProcessRunner processRunner)
    : GitEffectExecutorBase(repository, processRunner)
{
    public override EffectExecutorKey Key => GitEffectExecutorKeys.ParentGitlinkCommit;

    public override async Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        GitEffectPayload payload = Payload(intent);
        string directory = WorkingDirectory(payload);
        string pathspec = payload.Pathspec ?? ".agents";
        string before = await HeadAsync(directory);
        ProcessRunResult add = await GitAsync(directory, ["add", "--", pathspec]);
        if (add.ExitCode != 0) return Failure(add, "git add -- pathspec");
        ProcessRunResult staged = await GitAsync(directory, ["diff", "--cached", "--quiet", "--", pathspec]);
        if (staged.ExitCode == 0)
            return new(EffectLifecycle.Succeeded, "Parent gitlink already recorded.", [before], before, before, true, before);
        if (staged.ExitCode != 1) return Failure(staged, "git diff --cached --quiet -- pathspec");
        ProcessRunResult commit = await GitAsync(directory, ["commit", "-m", payload.CommitMessage]);
        if (commit.ExitCode != 0) return Failure(commit, "git commit");
        string after = await HeadAsync(directory);
        return new(EffectLifecycle.Succeeded, "Parent gitlink commit created.", [after], before, after, after != "unborn", after);
    }
}

public sealed class ParentWorkingTreeCommitEffectExecutor(Repository repository, IProcessRunner processRunner)
    : GitEffectExecutorBase(repository, processRunner)
{
    public override EffectExecutorKey Key => GitEffectExecutorKeys.ParentWorkingTreeCommit;

    public override async Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        GitEffectPayload payload = Payload(intent);
        string directory = WorkingDirectory(payload);
        string before = await HeadAsync(directory);
        string[] pathspec = [".", ":(exclude).agents"];
        ProcessRunResult status = await GitAsync(directory, ["status", "--porcelain", "--", .. pathspec]);
        if (status.ExitCode != 0) return Failure(status, "git status -- worktree");
        if (string.IsNullOrWhiteSpace(status.StandardOutput))
            return new(EffectLifecycle.Succeeded, "Parent working tree already clean.", [before], before, before, true, before);
        ProcessRunResult add = await GitAsync(directory, ["add", "-A", "--", .. pathspec]);
        if (add.ExitCode != 0) return Failure(add, "git add -- worktree");
        ProcessRunResult commit = await GitAsync(directory, ["commit", "-m", payload.CommitMessage]);
        if (commit.ExitCode != 0) return Failure(commit, "git commit");
        string after = await HeadAsync(directory);
        return new(EffectLifecycle.Succeeded, "Parent working-tree commit created.", [after], before, after, after != "unborn", after);
    }
}

public sealed class GitEffectReconciler(Repository _repository, IProcessRunner _processRunner) : IEffectReconciler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<EffectReconciliationObservation> ReconcileAsync(EffectIntent intent, CancellationToken cancellationToken)
    {
        GitEffectPayload payload = JsonSerializer.Deserialize<GitEffectPayload>(intent.TypedPayload, JsonOptions)
            ?? throw new InvalidOperationException("Git effect payload is invalid.");
        string root = Path.GetFullPath(_repository.Path);
        string directory = Path.GetFullPath(Path.Combine(root,
            payload.RepositoryRelativeWorkingDirectory.Replace('/', Path.DirectorySeparatorChar)));
        ProcessRunResult head = await _processRunner.RunAsync("git", ["rev-parse", "HEAD"], directory);
        if (head.ExitCode != 0)
            return new(EffectReconciliationVerdict.StillUnknown, "Local HEAD cannot be observed.",
                [$"exit:{head.ExitCode}"], "unknown", "unknown");
        string local = head.StandardOutput.Trim();
        if (intent.Executor == GitEffectExecutorKeys.NestedRepositoryPush ||
            intent.Executor == GitEffectExecutorKeys.ParentRepositoryPush)
        {
            ProcessRunResult upstream = await _processRunner.RunAsync("git", ["rev-parse", "@{u}"], directory);
            string remote = upstream.ExitCode == 0 ? upstream.StandardOutput.Trim() : "missing-upstream";
            return string.Equals(local, remote, StringComparison.OrdinalIgnoreCase)
                ? new(EffectReconciliationVerdict.Succeeded, "Remote ref independently matches HEAD.", [local], local, remote, local)
                : new(EffectReconciliationVerdict.NotApplied, "Remote ref does not match HEAD.", [local, remote], local, remote);
        }
        IReadOnlyList<string> statusArguments = intent.Executor == GitEffectExecutorKeys.ParentWorkingTreeCommit
            ? ["status", "--porcelain", "--", ".", ":(exclude).agents"]
            : payload.Pathspec is null
                ? ["status", "--porcelain"]
                : ["status", "--porcelain", "--", payload.Pathspec];
        ProcessRunResult status = await _processRunner.RunAsync("git", statusArguments, directory);
        bool clean = status.ExitCode == 0 && string.IsNullOrWhiteSpace(status.StandardOutput);
        return clean
            ? new(EffectReconciliationVerdict.Succeeded, "Committed target is independently clean.", [local], "unknown", local, local)
            : new(EffectReconciliationVerdict.NotApplied, "Target still has uncommitted changes.", [status.StandardOutput], local, local);
    }
}

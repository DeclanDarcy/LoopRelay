using System.Text.Json;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Infrastructure.Tests.Services.Effects;

public sealed class GitEffectExecutorsTests
{
    [Fact]
    public async Task Commit_pathspec_stages_only_the_declared_input_surface()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-scoped-commit-").FullName;
        var process = new ProcessRunner();
        await GitAsync(process, root, "init");
        await GitAsync(process, root, "config", "user.email", "tests@looprelay.local");
        await GitAsync(process, root, "config", "user.name", "LoopRelay Tests");
        Directory.CreateDirectory(Path.Combine(root, "declared"));
        Directory.CreateDirectory(Path.Combine(root, "outside"));
        await File.WriteAllTextAsync(Path.Combine(root, "declared", "input.md"), "declared");
        await File.WriteAllTextAsync(Path.Combine(root, "outside", "notes.md"), "outside");
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
        string payload = JsonSerializer.Serialize(
            new GitEffectPayload(".", "commit declared surface", "declared"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        EffectIntent intent = GitIntent(
            GitEffectExecutorKeys.NestedRepositoryCommit, "scoped-commit", Causality(), payload);

        EffectExecutionObservation result = await new NestedRepositoryCommitEffectExecutor(repository, process)
            .ExecuteAsync(intent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, result.State);
        ProcessRunResult status = await process.RunAsync("git", ["status", "--porcelain"], root);
        Assert.DoesNotContain("declared/input.md", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("outside/", status.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostReceiptReconcilesRealCommitWithoutCreatingSecondCommit()
    {
        (Repository repository, ProcessRunner process) = await CreateRepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".agents", "plan.md"), "plan");
        EffectIntent intent = Intent(repository, GitEffectExecutorKeys.NestedRepositoryCommit, "nested-commit");
        var durable = new CanonicalEffectWorkStore(repository);
        await durable.AppendPlanAsync([intent], CancellationToken.None);
        var faulted = new FailFirstReceiptStore(durable);
        var executor = new NestedRepositoryCommitEffectExecutor(repository, process);
        var reconciler = new GitEffectReconciler(repository, process);

        await new EffectWorker("before-crash", faulted, new EffectExecutorRegistry([executor]), reconciler,
            TimeSpan.FromMinutes(1)).RunOnceAsync();
        Assert.Equal(EffectLifecycle.Unknown, (await durable.ReadAsync(intent.Identity, CancellationToken.None))!.State);
        Assert.Equal(1, await CommitCountAsync(process, Path.Combine(repository.Path, ".agents")));

        await new EffectWorker("after-restart", durable, new EffectExecutorRegistry([executor]), reconciler,
            TimeSpan.FromMinutes(1)).RunOnceAsync();

        Assert.Equal(1, await CommitCountAsync(process, Path.Combine(repository.Path, ".agents")));
        EffectWorkItem settled = (await durable.ReadAsync(intent.Identity, CancellationToken.None))!;
        Assert.Equal(EffectLifecycle.Succeeded, settled.State);
        Assert.NotNull(settled.Receipt);
    }

    [Fact]
    public async Task UnavailableRemoteLeavesRequiredPushPendingAndPlanUnsettled()
    {
        (Repository repository, ProcessRunner process) = await CreateRepositoryAsync();
        CanonicalCausalContext causality = Causality();
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".agents", "details.md"), "details");
        EffectIntent commit = Intent(repository, GitEffectExecutorKeys.NestedRepositoryCommit, "nested-commit", causality);
        EffectIntent push = Intent(repository, GitEffectExecutorKeys.NestedRepositoryPush, "nested-push", causality,
            [commit.Identity], EffectRequiredness.RequiredAsync, order: 1);
        var store = new CanonicalEffectWorkStore(repository);
        await store.AppendPlanAsync([commit, push], CancellationToken.None);
        var commitExecutor = new NestedRepositoryCommitEffectExecutor(repository, process);
        var pushExecutor = new GitPushEffectExecutor(repository, process, GitEffectExecutorKeys.NestedRepositoryPush);
        var reconciler = new GitEffectReconciler(repository, process);
        var worker = new EffectWorker("push-test", store,
            new EffectExecutorRegistry([commitExecutor, pushExecutor]), reconciler, TimeSpan.FromMinutes(1));
        var settlement = new RecordingSettlement();

        TransitionEffectCoordinationResult result = await new TransitionEffectCoordinator(store, worker, settlement)
            .CoordinateAsync(causality.TransitionRun, CancellationToken.None);

        IReadOnlyList<EffectWorkItem> plan = await store.ReadPlanAsync(causality.TransitionRun, CancellationToken.None);
        Assert.Equal(EffectLifecycle.Succeeded, plan[0].State);
        Assert.Equal(EffectLifecycle.Pending, plan[1].State);
        Assert.Equal(RuntimeOutcomeKind.EffectsPending, result.Outcome);
        Assert.True(result.RequiredEffectsPending);
        Assert.Equal(0, settlement.Calls);
    }

    [Fact]
    public async Task ParentGitlinkCommitAndPushAreSeparateSemanticMutations()
    {
        (Repository repository, ProcessRunner process) = await CreateRepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(repository.Path, ".agents", "context.md"), "context");
        EffectIntent nested = Intent(repository, GitEffectExecutorKeys.NestedRepositoryCommit, "nested-commit");
        Assert.Equal(EffectLifecycle.Succeeded,
            (await new NestedRepositoryCommitEffectExecutor(repository, process)
                .ExecuteAsync(nested, CancellationToken.None)).State);
        CanonicalCausalContext causality = nested.Causality;
        var parentPayload = new GitEffectPayload(".", "record gitlink", ".agents");
        EffectIntent parent = GitIntent(
            GitEffectExecutorKeys.ParentGitlinkCommit, "parent-gitlink", causality,
            JsonSerializer.Serialize(parentPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        EffectExecutionObservation commit = await new ParentGitlinkCommitEffectExecutor(repository, process)
            .ExecuteAsync(parent, CancellationToken.None);
        EffectIntent push = GitIntent(
            GitEffectExecutorKeys.ParentRepositoryPush, "parent-push", causality,
            JsonSerializer.Serialize(new GitEffectPayload(".", "push parent"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        EffectExecutionObservation remote = await new GitPushEffectExecutor(
            repository, process, GitEffectExecutorKeys.ParentRepositoryPush)
            .ExecuteAsync(push, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, commit.State);
        Assert.Equal(1, await CommitCountAsync(process, repository.Path));
        Assert.Equal(EffectLifecycle.Pending, remote.State);
    }

    [Fact]
    public async Task Parent_gitlink_commit_records_new_nested_head_when_an_unrelated_nested_file_remains_dirty()
    {
        (Repository repository, ProcessRunner process) = await CreateRepositoryAsync();
        string agents = Path.Combine(repository.Path, ".agents");
        await File.WriteAllTextAsync(Path.Combine(agents, "plan.md"), "initial plan");
        await GitAsync(process, agents, "add", ".");
        await GitAsync(process, agents, "commit", "-m", "seed agents");
        await GitAsync(process, repository.Path, "add", ".agents");
        await GitAsync(process, repository.Path, "commit", "-m", "seed parent gitlink");
        await File.WriteAllTextAsync(Path.Combine(agents, "plan.md"), "revised but unpublished plan");
        await File.WriteAllTextAsync(Path.Combine(agents, "operational_context.md"), "context");

        CanonicalCausalContext causality = Causality();
        string nestedPayload = JsonSerializer.Serialize(
            new GitEffectPayload(".agents", "publish context", "operational_context.md"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        EffectIntent nested = GitIntent(
            GitEffectExecutorKeys.NestedRepositoryCommit, "nested-context", causality, nestedPayload);
        EffectExecutionObservation nestedResult = await new NestedRepositoryCommitEffectExecutor(repository, process)
            .ExecuteAsync(nested, CancellationToken.None);
        string parentPayload = JsonSerializer.Serialize(
            new GitEffectPayload(".", "record gitlink", ".agents"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        EffectIntent parent = GitIntent(
            GitEffectExecutorKeys.ParentGitlinkCommit, "parent-gitlink", causality, parentPayload);
        EffectExecutionObservation parentResult = await new ParentGitlinkCommitEffectExecutor(repository, process)
            .ExecuteAsync(parent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, nestedResult.State);
        Assert.Equal(EffectLifecycle.Succeeded, parentResult.State);
        Assert.Equal(2, await CommitCountAsync(process, repository.Path));
        ProcessRunResult nestedStatus = await process.RunAsync("git", ["status", "--porcelain"], agents);
        Assert.Contains("plan.md", nestedStatus.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("operational_context.md", nestedStatus.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parent_gitlink_commit_is_idempotent_when_only_the_nested_worktree_is_dirty()
    {
        (Repository repository, ProcessRunner process) = await CreateRepositoryAsync();
        string agents = Path.Combine(repository.Path, ".agents");
        await File.WriteAllTextAsync(Path.Combine(agents, "plan.md"), "initial plan");
        await GitAsync(process, agents, "add", ".");
        await GitAsync(process, agents, "commit", "-m", "seed agents");
        await GitAsync(process, repository.Path, "add", ".agents");
        await GitAsync(process, repository.Path, "commit", "-m", "seed parent gitlink");
        await File.WriteAllTextAsync(Path.Combine(agents, "unpublished.md"), "not part of this effect");
        CanonicalCausalContext causality = Causality();
        string payload = JsonSerializer.Serialize(
            new GitEffectPayload(".", "record gitlink", ".agents"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        EffectIntent parent = GitIntent(
            GitEffectExecutorKeys.ParentGitlinkCommit, "parent-gitlink", causality, payload);

        EffectExecutionObservation result = await new ParentGitlinkCommitEffectExecutor(repository, process)
            .ExecuteAsync(parent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, result.State);
        Assert.Equal("Parent gitlink already recorded.", result.Explanation);
        Assert.Equal(1, await CommitCountAsync(process, repository.Path));
        ProcessRunResult nestedStatus = await process.RunAsync("git", ["status", "--porcelain"], agents);
        Assert.Contains("unpublished.md", nestedStatus.StandardOutput, StringComparison.Ordinal);
    }

    private static EffectIntent Intent(
        Repository repository,
        EffectExecutorKey executor,
        string operation,
        CanonicalCausalContext? causality = null,
        IReadOnlyList<EffectIntentIdentity>? dependencies = null,
        EffectRequiredness requiredness = EffectRequiredness.BlockingLocal,
        int order = 0)
    {
        var payload = new GitEffectPayload(".agents", "test publication");
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EffectIntent(
            EffectIntentIdentity.New(), causality ?? Causality(), operation, executor, "1",
            new EffectTargetDescriptor("git", ".agents", "{}"), json, new string('a', 64), order,
            dependencies ?? [], requiredness, new EffectCondition("repository", "{}"),
            new EffectCondition("git", "{}"), "observe-before-repeat", $"{operation}:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
    }

    private static EffectIntent GitIntent(
        EffectExecutorKey executor,
        string operation,
        CanonicalCausalContext causality,
        string payload) => new(
            EffectIntentIdentity.New(), causality, operation, executor, "1",
            new EffectTargetDescriptor("git", ".", "{}"), payload, new string('b', 64), 0, [],
            EffectRequiredness.BlockingLocal, new EffectCondition("repository", "{}"),
            new EffectCondition("git", "{}"), "observe-before-repeat", $"{operation}:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);

    private static CanonicalCausalContext Causality() => new(
        WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(), AttemptIdentity.New());

    private static async Task<(Repository Repository, ProcessRunner Process)> CreateRepositoryAsync()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-git-effect-").FullName;
        string agents = Directory.CreateDirectory(Path.Combine(root, ".agents")).FullName;
        var process = new ProcessRunner();
        await GitAsync(process, root, "init");
        await GitAsync(process, root, "config", "user.email", "tests@looprelay.local");
        await GitAsync(process, root, "config", "user.name", "LoopRelay Tests");
        await GitAsync(process, agents, "init");
        await GitAsync(process, agents, "config", "user.email", "tests@looprelay.local");
        await GitAsync(process, agents, "config", "user.name", "LoopRelay Tests");
        return (new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root }, process);
    }

    private static async Task GitAsync(ProcessRunner process, string directory, params string[] args)
    {
        ProcessRunResult result = await process.RunAsync("git", args, directory);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static async Task<int> CommitCountAsync(ProcessRunner process, string directory)
    {
        ProcessRunResult result = await process.RunAsync("git", ["rev-list", "--count", "HEAD"], directory);
        Assert.Equal(0, result.ExitCode);
        return int.Parse(result.StandardOutput.Trim());
    }

    private sealed class RecordingSettlement : IEffectPlanSettlementStore
    {
        public int Calls { get; private set; }
        public Task<bool> TrySettleAsync(TransitionRunIdentity transitionRun, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(false);
        }

        public Task RecordOutcomeAsync(
            TransitionRunIdentity transitionRun,
            RuntimeOutcomeKind outcome,
            string explanation,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailFirstReceiptStore(IEffectWorkStore inner) : IEffectWorkStore
    {
        private bool _failed;
        public Task<IReadOnlyList<EffectWorkItem>> ScanUnsettledAsync(int limit, DateTimeOffset now, CancellationToken token) => inner.ScanUnsettledAsync(limit, now, token);
        public Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(TransitionRunIdentity transition, CancellationToken token) => inner.ReadPlanAsync(transition, token);
        public Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken token) => inner.ReadAsync(identity, token);
        public Task<EffectLease?> TryLeaseAsync(EffectIntentIdentity identity, long version, string worker, DateTimeOffset now, TimeSpan duration, CancellationToken token) => inner.TryLeaseAsync(identity, version, worker, now, duration, token);
        public Task<EffectWorkItem> AppendLifecycleAsync(EffectIntentIdentity identity, long version, EffectLifecycle state, string worker, string explanation, IReadOnlyList<string> evidence, DateTimeOffset at, CancellationToken token) => inner.AppendLifecycleAsync(identity, version, state, worker, explanation, evidence, at, token);
        public Task RecordReconciliationAsync(EffectIntentIdentity identity, long version, EffectReconciliationObservation observation, string worker, DateTimeOffset at, CancellationToken token) => inner.RecordReconciliationAsync(identity, version, observation, worker, at, token);
        public Task<EffectWorkItem> RecordReceiptAsync(EffectIntentIdentity identity, long version, EffectReceipt receipt, string worker, CancellationToken token)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Injected receipt persistence loss.");
            }
            return inner.RecordReceiptAsync(identity, version, receipt, worker, token);
        }
    }
}

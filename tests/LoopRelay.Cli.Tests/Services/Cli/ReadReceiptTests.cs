using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class ReadReceiptTests
{
    [Fact]
    public async Task Receipt_records_commit_surface_tree_hashes_files_and_products_in_a_git_workspace()
    {
        string repo = TempRepo();
        Repository repository = RepositoryFor(repo);
        await GitAsync(repo, "init");
        await GitAsync(repo, "config", "user.email", "tests@looprelay.local");
        await GitAsync(repo, "config", "user.name", "LoopRelay Tests");
        Write(repo, ".agents/epic.md", "# Epic: Receipts");
        Write(repo, ".agents/specs/receipts.md", "spec body");
        await GitAsync(repo, "add", ".");
        await GitAsync(repo, "commit", "-m", "inputs");
        string expectedCommit = (await GitReadAsync(repo, "rev-parse", "HEAD")).Trim();
        string expectedTree = (await GitReadAsync(repo, "rev-parse", "HEAD:.agents")).Trim();
        WorkflowTransitionDefinition definition = WriteExecutablePlanDefinition();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new UnifiedCliComposition.CanonicalReadReceiptStore(persistence, new ProcessRunner(), repository);
        CanonicalCausalContext causality = Attempt();
        var request = new TransitionRuntimeRequest(
            WorkflowIdentity.Plan,
            new WorkflowStageIdentity("Planning"),
            definition.Identity,
            ExecutionContext(),
            FreshAttemptAuthorization.Instance);
        var consumedProduct = new ProductRecord(
            ProductIdentity.PreparedEpic,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity("ObservedPreparedEpic"),
            [WorkflowIdentity.Plan],
            "repository-owned observed artifact evidence",
            "repository observation",
            [".agents/epic.md"],
            "content-hash",
            ProductFreshness.Fresh,
            ProductValidationState.Unknown,
            ProductLifecycle.Active,
            [".agents/epic.md"]);

        await store.AppendAsync(
            new ReadReceiptCapture(
                causality,
                request,
                definition,
                [ConsumedInputFile.FromContent(".agents/epic.md", "# Epic: Receipts")],
                [consumedProduct],
                "Usable",
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        CanonicalReadReceiptRecord receipt = Assert.Single(await persistence.ReadReadReceiptsAsync());
        Assert.StartsWith("rcpt_", receipt.ReceiptId, StringComparison.Ordinal);
        Assert.Equal(causality.Run.Value, receipt.RunId);
        Assert.Equal("Plan", receipt.WorkflowIdentity);
        Assert.Equal("WriteExecutablePlan", receipt.TransitionIdentity);
        Assert.Equal(causality.Attempt.Value, receipt.AttemptId);
        Assert.Equal(expectedCommit, receipt.CommitHash);
        string surface = Assert.Single(receipt.InputSurfaces);
        Assert.Equal(".agents/", surface);
        Assert.NotNull(receipt.SurfaceTreeHashes);
        Assert.Equal(expectedTree, receipt.SurfaceTreeHashes![".agents/"]);
        CanonicalReadReceiptFile file = Assert.Single(receipt.Files);
        Assert.Equal(".agents/epic.md", file.Path);
        Assert.Equal(ConsumedInputFile.HashContent("# Epic: Receipts"), file.Sha256);
        CanonicalReadReceiptProduct product = Assert.Single(receipt.Products);
        Assert.Equal("PreparedEpic", product.Identity);
        Assert.Equal("content-hash", product.CausalIdentity);
        Assert.Equal("Unknown", product.ValidationState);
        Assert.Equal("Usable", receipt.Validation);
    }

    [Fact]
    public async Task Receipt_degrades_to_null_git_provenance_outside_a_git_workspace()
    {
        string repo = TempRepo();
        Repository repository = RepositoryFor(repo);
        WorkflowTransitionDefinition definition = WriteExecutablePlanDefinition();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new UnifiedCliComposition.CanonicalReadReceiptStore(persistence, new ProcessRunner(), repository);
        CanonicalCausalContext causality = Attempt();
        var request = new TransitionRuntimeRequest(
            WorkflowIdentity.Plan,
            new WorkflowStageIdentity("Planning"),
            definition.Identity,
            ExecutionContext(),
            FreshAttemptAuthorization.Instance);

        await store.AppendAsync(
            new ReadReceiptCapture(
                causality,
                request,
                definition,
                [],
                [],
                "Plan prompt context is unavailable: sources missing.",
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        CanonicalReadReceiptRecord receipt = Assert.Single(await persistence.ReadReadReceiptsAsync());
        Assert.Null(receipt.CommitHash);
        Assert.Equal(causality.Attempt.Value, receipt.AttemptId);
        Assert.Equal(causality.Run.Value, receipt.RunId);
        Assert.NotNull(receipt.SurfaceTreeHashes);
        Assert.Null(receipt.SurfaceTreeHashes![".agents/"]);
        Assert.Empty(receipt.Files);
        Assert.Contains("unavailable", receipt.Validation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staleness_projection_reports_drift_only_for_changed_or_deleted_consumed_files()
    {
        string repo = TempRepo();
        Repository repository = RepositoryFor(repo);
        Write(repo, ".agents/plan.md", "original plan");
        Write(repo, ".agents/epic.md", "original epic");
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        await persistence.AppendReadReceiptAsync(new CanonicalReadReceiptRecord(
            CausalUlid.NewId("rcpt"),
            string.Empty,
            "Plan",
            "WriteExecutablePlan",
            null,
            null,
            [".agents/"],
            null,
            [
                new CanonicalReadReceiptFile(".agents/plan.md", ConsumedInputFile.HashContent("original plan")),
                new CanonicalReadReceiptFile(".agents/epic.md", ConsumedInputFile.HashContent("original epic")),
            ],
            [],
            "Usable",
            DateTimeOffset.UtcNow));

        IReadOnlyList<ConsumedInputDrift> unchanged = await ReadReceiptStaleness.ProjectAsync(repository, CancellationToken.None);
        Assert.Empty(unchanged);

        Write(repo, ".agents/plan.md", "edited plan");
        File.Delete(Path.Combine(repo, ".agents", "epic.md"));
        IReadOnlyList<ConsumedInputDrift> drift = await ReadReceiptStaleness.ProjectAsync(repository, CancellationToken.None);

        Assert.Equal(2, drift.Count);
        ConsumedInputDrift edited = Assert.Single(drift, entry => entry.Path == ".agents/plan.md");
        Assert.Equal(ConsumedInputFile.HashContent("original plan"), edited.ConsumedSha256);
        Assert.Equal(ConsumedInputFile.HashContent("edited plan"), edited.CurrentSha256);
        ConsumedInputDrift deleted = Assert.Single(drift, entry => entry.Path == ".agents/epic.md");
        Assert.Null(deleted.CurrentSha256);
        Assert.Equal("Plan", deleted.Workflow);
        Assert.Equal("WriteExecutablePlan", deleted.Transition);
    }

    [Fact]
    public async Task Staleness_projection_compares_only_the_latest_receipt_per_transition()
    {
        string repo = TempRepo();
        Repository repository = RepositoryFor(repo);
        Write(repo, ".agents/plan.md", "current plan");
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        await persistence.AppendReadReceiptAsync(Receipt(
            "Plan",
            "WriteExecutablePlan",
            ".agents/plan.md",
            "stale content",
            DateTimeOffset.UtcNow.AddMinutes(-5)));
        await persistence.AppendReadReceiptAsync(Receipt(
            "Plan",
            "WriteExecutablePlan",
            ".agents/plan.md",
            "current plan",
            DateTimeOffset.UtcNow));

        IReadOnlyList<ConsumedInputDrift> drift = await ReadReceiptStaleness.ProjectAsync(repository, CancellationToken.None);

        Assert.Empty(drift);
    }

    [Fact]
    public async Task Staleness_projection_ignores_receipt_paths_that_resolve_outside_the_repository()
    {
        string repo = TempRepo();
        Repository repository = RepositoryFor(repo);
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        await persistence.AppendReadReceiptAsync(Receipt(
            "Plan",
            "WriteExecutablePlan",
            "../outside.md",
            "anything",
            DateTimeOffset.UtcNow));

        IReadOnlyList<ConsumedInputDrift> drift = await ReadReceiptStaleness.ProjectAsync(repository, CancellationToken.None);

        Assert.Empty(drift);
    }

    private static CanonicalReadReceiptRecord Receipt(
        string workflow,
        string transition,
        string path,
        string content,
        DateTimeOffset consumedAt) =>
        new(
            CausalUlid.NewId("rcpt"),
            string.Empty,
            workflow,
            transition,
            null,
            null,
            [],
            null,
            [new CanonicalReadReceiptFile(path, ConsumedInputFile.HashContent(content))],
            [],
            "Usable",
            consumedAt);

    private static WorkflowTransitionDefinition WriteExecutablePlanDefinition()
    {
        WorkflowDefinition plan = CanonicalWorkflowDefinitionSketches.CreateAll()
            .Single(workflow => workflow.Identity == WorkflowIdentity.Plan);
        return plan.Transitions.Single(transition => transition.Identity.Value == "WriteExecutablePlan");
    }

    private static Repository RepositoryFor(string repositoryPath) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repositoryPath),
            Path = repositoryPath,
        };

    private static CanonicalTransitionExecutionContext ExecutionContext()
    {
        var invocation = new WorkflowInvocation(InvocationModeKind.BoundedPlan);
        return new CanonicalTransitionExecutionContext(
            invocation,
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test"));
    }

    private static CanonicalCausalContext Attempt() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    private static string TempRepo() =>
        Directory.CreateTempSubdirectory("cc-cli-read-receipts").FullName;

    private static void Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task GitAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.Equal(0, result.ExitCode);
    }

    private static async Task<string> GitReadAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput;
    }
}

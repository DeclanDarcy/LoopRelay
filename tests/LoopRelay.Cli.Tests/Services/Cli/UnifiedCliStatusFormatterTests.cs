using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedCliStatusFormatterTests
{
    [Fact]
    public void Format_explains_invocation_selection_stage_transition_gates_blockers_and_storage()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-status").FullName;
        var invocation = new UnifiedCliInvocation(
            new Repository
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(repo),
                Path = repo,
            },
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Status, []));
        RepositoryObservation observation = Observation(
            [Observed(ProductIdentity.PreparedEpic), Observed(ProductIdentity.MilestoneSpecificationSet)]);
        WorkflowResolutionResult resolution = new WorkflowResolver().Resolve(
            invocation.WorkflowInvocation,
            observation,
            CanonicalWorkflowDefinitionSketches.CreateAll());

        string status = UnifiedCliStatusFormatter.Format(invocation, observation, resolution);

        Assert.Contains("Invocation mode: BoundedPlan", status, StringComparison.Ordinal);
        Assert.Contains("Selected chain: BoundedPlan", status, StringComparison.Ordinal);
        Assert.Contains("Selected workflow: Plan", status, StringComparison.Ordinal);
        Assert.Contains("Current stage: Planning", status, StringComparison.Ordinal);
        Assert.Contains("Next eligible transition: WriteExecutablePlan", status, StringComparison.Ordinal);
        Assert.Contains("Satisfied gates:", status, StringComparison.Ordinal);
        Assert.Contains("Unsatisfied gates:", status, StringComparison.Ordinal);
        Assert.Contains("Blockers:", status, StringComparison.Ordinal);
        Assert.Contains("Storage authority: FilesystemExport", status, StringComparison.Ordinal);
        Assert.Contains("User action required:", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_exposes_authoritative_continuity_ids_ancestry_and_operator_action_without_content()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-continuity-status").FullName;
        var invocation = new UnifiedCliInvocation(
            new Repository { Id = Guid.NewGuid(), Name = "repo", Path = repo },
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Status, []));
        RepositoryObservation observation = Observation(
            [Observed(ProductIdentity.PreparedEpic), Observed(ProductIdentity.MilestoneSpecificationSet)]);
        WorkflowResolutionResult resolution = new WorkflowResolver().Resolve(
            invocation.WorkflowInvocation, observation, CanonicalWorkflowDefinitionSketches.CreateAll());
        DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var lineage = new DecisionSessionLineageNode(
            "lineage-child", "scope-1", "codex", "thread-child", "lineage-parent", "lineage-parent",
            "RepositoryOnly", RecoveryCompleteness.RepositoryOnly, "source-digest", "profile-digest",
            "plan-digest", now, now, null, "Authoritative");
        var active = new DecisionSessionActiveState(
            "scope-1", lineage.LineageId, new DecisionSessionAccounting(0, 0, 0, 0, 0, 0, 0, null, 0),
            "policy", null, 2, now);
        var snapshot = new DecisionContinuityStatusSnapshot(
            1, active, lineage, [lineage], null, null, null, null);

        string status = UnifiedCliStatusFormatter.Format(invocation, observation, resolution, snapshot);

        Assert.Contains("Continuity active scope: scope-1", status, StringComparison.Ordinal);
        Assert.Contains("Continuity provider thread: thread-child", status, StringComparison.Ordinal);
        Assert.Contains("Continuity completeness: RepositoryOnly", status, StringComparison.Ordinal);
        Assert.Contains("Continuity ancestry: lineage-child:thread-child:RepositoryOnly", status, StringComparison.Ordinal);
        Assert.Contains("Continuity operator action: (none)", status, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", status, StringComparison.OrdinalIgnoreCase);
    }

    private static RepositoryObservation Observation(IReadOnlyList<ObservedProduct> products)
    {
        var storage = new StorageVerificationResult(
            StorageAuthorityKind.FilesystemExport,
            UsableAuthority: true,
            StaleExports: [],
            Conflicts: [],
            Corruption: [],
            UnsupportedSchema: [],
            UnresolvedReferences: [],
            PartialTransactions: [],
            BlockingConditions: [],
            Evidence: [".agents"]);
        return new RepositoryObservation(
            "repo",
            new StorageAuthoritySnapshot(
                storage.Authority,
                storage.UsableAuthority,
                storage.UsableAuthority ? "test" : "blocked",
                storage.Evidence),
            WorkflowStates: [],
            Products: products,
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: true, HasWorkingTreeChanges: false, CurrentBranch: "main", Evidence: [".git"]),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: storage);
    }

    private static ObservedProduct Observed(ProductIdentity identity)
    {
        var record = new ProductRecord(
            identity,
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [WorkflowIdentity.Plan],
            "repository-owned test evidence",
            "test",
            [$"{identity}.md"],
            $"hash-{identity}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity}.md"]);
        return new ObservedProduct(record, GateUsable: true, record.EvidenceLocations);
    }
}

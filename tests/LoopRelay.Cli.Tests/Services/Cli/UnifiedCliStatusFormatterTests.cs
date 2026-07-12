using LoopRelay.Cli.Services.Application;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedCliStatusFormatterTests
{
    [Fact]
    public void Format_renders_the_immutable_application_snapshot()
    {
        CanonicalCliStatusSnapshot snapshot = Snapshot();

        string status = UnifiedCliStatusFormatter.Format(snapshot);

        Assert.Contains("Invocation mode: BoundedPlan", status, StringComparison.Ordinal);
        Assert.Contains("Selected chain: BoundedPlan", status, StringComparison.Ordinal);
        Assert.Contains("Selected workflow: Plan", status, StringComparison.Ordinal);
        Assert.Contains("Current stage: Planning", status, StringComparison.Ordinal);
        Assert.Contains("Next eligible transition: WriteExecutablePlan", status, StringComparison.Ordinal);
        Assert.Contains("Storage authority: FilesystemExport", status, StringComparison.Ordinal);
        Assert.Contains("User action required: inspect plan inputs", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_renders_warnings_and_input_drift_without_querying_authority_stores()
    {
        var warning = new ResolutionWarning(
            WarningCategory.Repository,
            "Input surface has uncommitted changes.",
            "canonical gate authority",
            "Commit the listed files before rerunning.",
            [".agents/epic.md"]);
        CanonicalCliStatusSnapshot baseline = Snapshot(warning);
        CanonicalCliStatusSnapshot snapshot = baseline with
        {
            InputDrift =
            [
                new ConsumedInputDrift(
                    ".agents/plan.md", "aaaabbbbccccdddd", "eeeeffff00001111", "Plan", "WriteExecutablePlan"),
                new ConsumedInputDrift(
                    ".agents/epic.md", "1111222233334444", null, "Plan", "WriteExecutablePlan"),
            ],
        };

        string status = UnifiedCliStatusFormatter.Format(snapshot);

        Assert.Contains("Warnings:", status, StringComparison.Ordinal);
        Assert.Contains("Remediation: Commit the listed files before rerunning.", status, StringComparison.Ordinal);
        Assert.Contains("Inputs changed since last consumption:", status, StringComparison.Ordinal);
        Assert.Contains(".agents/plan.md: consumed aaaabbbb now eeeeffff", status, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md: consumed 11112222 now missing", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_renders_continuity_pending_work_policy_and_compatibility_evidence()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var lineage = new DecisionSessionLineageNode(
            "lineage-child", "scope-1", "codex", "thread-child", "lineage-parent", "lineage-parent",
            "RepositoryOnly", RecoveryCompleteness.RepositoryOnly, "source-digest", "profile-digest",
            "plan-digest", now, now, null, "Authoritative");
        var active = new DecisionSessionActiveState(
            "scope-1", lineage.LineageId, new DecisionSessionAccounting(0, 0, 0, 0, 0, 0, 0, null, 0),
            "policy", null, 2, now);
        CanonicalCliStatusSnapshot snapshot = Snapshot() with
        {
            Continuity = new DecisionContinuityStatusSnapshot(
                1, active, lineage, [lineage], null, null, null, null),
            PendingEffects = ["persist-plan"],
            PendingDispatches = ["dispatch-1:Prepared"],
            PolicyEvaluations = ["runtime-eval-1:Accepted"],
            Compatibility = ["legacy export requires import"],
        };

        string status = UnifiedCliStatusFormatter.Format(snapshot);

        Assert.Contains("Continuity active scope: scope-1", status, StringComparison.Ordinal);
        Assert.Contains("Continuity provider thread: thread-child", status, StringComparison.Ordinal);
        Assert.Contains("Continuity ancestry: lineage-child:thread-child:RepositoryOnly", status, StringComparison.Ordinal);
        Assert.Contains("Pending effects: persist-plan", status, StringComparison.Ordinal);
        Assert.Contains("Pending dispatches: dispatch-1:Prepared", status, StringComparison.Ordinal);
        Assert.Contains("Policy evaluations: runtime-eval-1:Accepted", status, StringComparison.Ordinal);
        Assert.Contains("Compatibility: legacy export requires import", status, StringComparison.Ordinal);
    }

    private static CanonicalCliStatusSnapshot Snapshot(ResolutionWarning? warning = null)
    {
        RepositoryObservation observation = Observation(warning);
        var invocation = new WorkflowInvocation(InvocationModeKind.BoundedPlan);
        WorkflowResolutionResult resolution = new WorkflowResolver().Resolve(
            invocation,
            observation,
            CanonicalWorkflowDefinitionSketches.CreateAll());
        return new CanonicalCliStatusSnapshot(
            "repo",
            observation,
            resolution,
            null,
            [],
            [],
            [],
            [],
            [],
            ["inspect plan inputs"]);
    }

    private static RepositoryObservation Observation(ResolutionWarning? warning)
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
        IReadOnlyList<ResolutionWarning> warnings = warning is null ? [] : [warning];
        return new RepositoryObservation(
            "repo",
            new StorageAuthoritySnapshot(storage.Authority, true, "test", storage.Evidence),
            WorkflowStates:
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    WorkflowResolutionState.Active,
                    new WorkflowStageIdentity("Planning"),
                    [],
                    warnings,
                    ["plan-state.md"]),
            ],
            Products: [Observed(ProductIdentity.PreparedEpic), Observed(ProductIdentity.MilestoneSpecificationSet)],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(true, false, "main", [".git"]),
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
        return new ObservedProduct(record, true, record.EvidenceLocations);
    }
}

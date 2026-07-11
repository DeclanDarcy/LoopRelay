using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedCliStatusFormatterTests
{
    [Fact]
    public void Format_explains_invocation_selection_stage_transition_gates_and_storage()
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
        Assert.DoesNotContain("Warnings", status, StringComparison.Ordinal);
        Assert.Contains("Storage authority: FilesystemExport", status, StringComparison.Ordinal);
        Assert.Contains("User action required:", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_renders_warnings_section_with_concern_and_remediation()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-status-warnings").FullName;
        var invocation = new UnifiedCliInvocation(
            new Repository
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(repo),
                Path = repo,
            },
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Status, []));
        var warning = new ResolutionWarning(
            WarningCategory.Repository,
            "Input surface '.agents/' has uncommitted changes.",
            "canonical gate authority",
            "Commit the listed files under '.agents/' before rerunning.",
            [".agents/epic.md"]);
        RepositoryObservation observation = Observation(
            [Observed(ProductIdentity.PreparedEpic), Observed(ProductIdentity.MilestoneSpecificationSet)],
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    WorkflowResolutionState.Active,
                    new WorkflowStageIdentity("Planning"),
                    [],
                    [warning],
                    ["plan-state.md"]),
            ]);
        WorkflowResolutionResult resolution = new WorkflowResolver().Resolve(
            invocation.WorkflowInvocation,
            observation,
            CanonicalWorkflowDefinitionSketches.CreateAll());

        string status = UnifiedCliStatusFormatter.Format(invocation, observation, resolution);

        Assert.Contains("Warnings:", status, StringComparison.Ordinal);
        Assert.Contains("Repository: Input surface '.agents/' has uncommitted changes.", status, StringComparison.Ordinal);
        Assert.Contains("Remediation: Commit the listed files under '.agents/' before rerunning.", status, StringComparison.Ordinal);
        Assert.Contains("User action required: Commit the listed files under '.agents/' before rerunning.", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_omits_warnings_section_when_there_are_no_warnings()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-status-no-warnings").FullName;
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
            [Observed(ProductIdentity.PreparedEpic), Observed(ProductIdentity.MilestoneSpecificationSet)],
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    WorkflowResolutionState.Completed,
                    null,
                    [],
                    [],
                    ["plan-state.md"]),
            ]);
        WorkflowResolutionResult resolution = new WorkflowResolver().Resolve(
            invocation.WorkflowInvocation,
            observation,
            CanonicalWorkflowDefinitionSketches.CreateAll());

        string status = UnifiedCliStatusFormatter.Format(invocation, observation, resolution);

        Assert.Empty(resolution.Explanation.Warnings);
        Assert.DoesNotContain("Warnings", status, StringComparison.Ordinal);
        Assert.Contains("User action required: (none)", status, StringComparison.Ordinal);
    }

    private static RepositoryObservation Observation(
        IReadOnlyList<ObservedProduct> products,
        IReadOnlyList<ObservedWorkflowState>? workflowStates = null)
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
                storage.UsableAuthority ? "test" : "unusable",
                storage.Evidence),
            WorkflowStates: workflowStates ?? [],
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

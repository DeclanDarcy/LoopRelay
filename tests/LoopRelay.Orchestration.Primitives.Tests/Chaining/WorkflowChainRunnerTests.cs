using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class WorkflowChainRunnerTests
{
    [Fact]
    public void Canonical_chain_definitions_cover_traditional_and_eval_routes()
    {
        Assert.Equal(
            [WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            CanonicalWorkflowChains.TraditionalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
        Assert.Equal(
            [WorkflowIdentity.EvalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            CanonicalWorkflowChains.EvalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public async Task Chain_progression_uses_validated_semantic_products_not_files()
    {
        FakeTransitionRuntime runtime = new();
        WorkflowChainRunner runner = CreateRunner(runtime, out WorkflowBoundaryEvidenceWriter evidence);
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                Completed(WorkflowIdentity.TraditionalRoadmap),
            ],
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);

        WorkflowChainRunResult result = await runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            CanonicalWorkflowChains.TraditionalRoadmapChain,
            Definitions));

        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.True(boundary.CanAdvance);
        Assert.Equal(WorkflowIdentity.Plan, boundary.TargetWorkflow);
        Assert.Equal(WorkflowStopReason.TransitionCompleted, result.StopReason);
        TransitionRuntimeRequest transitionRequest = Assert.Single(runtime.Requests);
        Assert.Equal(InvocationModeKind.ForcedTraditionalChain, transitionRequest.RootInvocation!.Mode);
        Assert.Equal(WorkflowIdentity.Plan, transitionRequest.Workflow);
        Assert.Single(evidence.Records);
    }

    [Fact]
    public async Task Boundary_blocks_when_files_may_exist_but_required_semantic_products_are_not_validated()
    {
        WorkflowChainRunner runner = CreateRunner(new FakeTransitionRuntime(), out _);
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                Completed(WorkflowIdentity.TraditionalRoadmap),
            ],
            products: []);

        WorkflowChainRunResult result = await runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            CanonicalWorkflowChains.TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.Blocked, result.StopReason);
        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.False(boundary.CanAdvance);
        Assert.False(boundary.ProductTransfer!.Gate.IsSatisfied);
    }

    [Fact]
    public async Task EvalRoadmap_boundary_blocks_when_plan_entry_products_are_invalid()
    {
        WorkflowChainRunner runner = CreateRunner(new FakeTransitionRuntime(), out _);
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                Completed(WorkflowIdentity.EvalRoadmap),
            ],
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.EvalRoadmap, ProductValidationState.Invalid),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.EvalRoadmap),
            ],
            evaluationIntentPaths: [".agents/evals/e1.md"]);

        WorkflowChainRunResult result = await runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedEvalChain),
            observation,
            CanonicalWorkflowChains.EvalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.Blocked, result.StopReason);
        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.False(boundary.CanAdvance);
        Assert.False(boundary.EntryGate!.IsSatisfied);
    }

    [Fact]
    public async Task Bounded_commands_stop_after_one_completed_workflow()
    {
        WorkflowChainRunner runner = CreateRunner(new FakeTransitionRuntime(), out _);
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                Completed(WorkflowIdentity.Plan),
            ],
            products:
            [
                Product(ProductIdentity.ExecutablePlan, WorkflowIdentity.Plan),
                Product(ProductIdentity.OperationalContext, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionDetails, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionMilestoneSet, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionReadiness, WorkflowIdentity.Plan),
            ]);

        WorkflowChainRunResult result = await runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation,
            CanonicalWorkflowChains.TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.BoundedWorkflowCompleted, result.StopReason);
        Assert.Empty(result.Boundaries);
    }

    [Fact]
    public async Task Execute_completion_ends_the_chain()
    {
        WorkflowChainRunner runner = CreateRunner(new FakeTransitionRuntime(), out _);
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                Completed(WorkflowIdentity.TraditionalRoadmap),
                Completed(WorkflowIdentity.Plan),
                Completed(WorkflowIdentity.Execute),
            ],
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.ExecutablePlan, WorkflowIdentity.Plan),
                Product(ProductIdentity.OperationalContext, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionDetails, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionMilestoneSet, WorkflowIdentity.Plan),
                Product(ProductIdentity.ExecutionReadiness, WorkflowIdentity.Plan),
                Product(ProductIdentity.CertifiedCompletion, WorkflowIdentity.Execute),
            ]);

        WorkflowChainRunResult result = await runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            CanonicalWorkflowChains.TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.ChainCompleted, result.StopReason);
        Assert.Equal(WorkflowIdentity.Execute, result.LastWorkflow);
        Assert.Equal(2, result.Boundaries.Count);
    }

    [Theory]
    [InlineData(InvocationModeKind.DefaultChained, true, "EvalRoadmapChain")]
    [InlineData(InvocationModeKind.DefaultChained, false, "TraditionalRoadmapChain")]
    [InlineData(InvocationModeKind.ForcedEvalChain, false, "EvalRoadmapChain")]
    [InlineData(InvocationModeKind.ForcedTraditionalChain, true, "TraditionalRoadmapChain")]
    public void Invocation_modes_select_the_expected_chain(
        InvocationModeKind mode,
        bool hasEvalIntent,
        string expectedChain)
    {
        RepositoryObservation observation = Observation(
            evaluationIntentPaths: hasEvalIntent ? [".agents/evals/e1.md"] : []);

        WorkflowSelectionResult selection = InvocationModeResolver.Resolve(
            new WorkflowInvocation(mode),
            observation);

        Assert.Equal(expectedChain, selection.SelectedChain);
    }

    [Theory]
    [InlineData(WorkflowResolutionState.Blocked, WorkflowStopReason.Blocked)]
    [InlineData(WorkflowResolutionState.Waiting, WorkflowStopReason.Waiting)]
    [InlineData(WorkflowResolutionState.Cancelled, WorkflowStopReason.Cancelled)]
    [InlineData(WorkflowResolutionState.Failed, WorkflowStopReason.Failed)]
    [InlineData(WorkflowResolutionState.Ambiguous, WorkflowStopReason.Ambiguous)]
    public async Task Controller_maps_terminal_resolution_states_to_stop_reasons(
        WorkflowResolutionState state,
        WorkflowStopReason stopReason)
    {
        WorkflowController controller = CreateController(new FakeTransitionRuntime());
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    state,
                    state == WorkflowResolutionState.Ambiguous ? null : new WorkflowStageIdentity("Planning"),
                    [],
                    state == WorkflowResolutionState.Blocked
                        ? [new ResolutionBlocker(BlockerCategory.Workflow, "blocked", "test", "fix", true, [])]
                        : [],
                    [$"{state}.json"]),
            ],
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);

        WorkflowControllerResult result = await controller.RunAsync(new WorkflowControllerRequest(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation,
            Definitions));

        Assert.Equal(stopReason, result.StopReason);
        Assert.Null(result.Transition);
    }

    [Fact]
    public async Task Controller_returns_no_eligible_transition_when_stage_has_only_blocked_transitions()
    {
        WorkflowController controller = CreateController(new FakeTransitionRuntime());
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    WorkflowResolutionState.Active,
                    new WorkflowStageIdentity("Planning"),
                    [],
                    [],
                    ["active.json"]),
            ],
            products: []);

        WorkflowControllerResult result = await controller.RunAsync(new WorkflowControllerRequest(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation,
            Definitions));

        Assert.Equal(WorkflowStopReason.NoEligibleTransition, result.StopReason);
        Assert.Null(result.Transition);
    }

    [Theory]
    [InlineData(RuntimeOutcomeKind.Blocked, WorkflowStopReason.Blocked)]
    [InlineData(RuntimeOutcomeKind.Waiting, WorkflowStopReason.Waiting)]
    [InlineData(RuntimeOutcomeKind.Cancelled, WorkflowStopReason.Cancelled)]
    [InlineData(RuntimeOutcomeKind.Failed, WorkflowStopReason.Failed)]
    [InlineData(RuntimeOutcomeKind.Stalled, WorkflowStopReason.Stalled)]
    [InlineData(RuntimeOutcomeKind.Ambiguous, WorkflowStopReason.Ambiguous)]
    public async Task Controller_maps_runtime_outcomes_to_stop_reasons(
        RuntimeOutcomeKind outcome,
        WorkflowStopReason expected)
    {
        FakeTransitionRuntime runtime = new()
        {
            Result = RuntimeResult(outcome),
        };
        WorkflowController controller = CreateController(runtime);
        RepositoryObservation observation = Observation(
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);

        WorkflowControllerResult result = await controller.RunAsync(new WorkflowControllerRequest(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation,
            Definitions));

        Assert.Equal(expected, result.StopReason);
        Assert.NotNull(result.Transition);
    }

    private static readonly IReadOnlyList<WorkflowDefinition> Definitions =
        CanonicalWorkflowDefinitionSketches.CreateAll();

    private static WorkflowChainRunner CreateRunner(
        FakeTransitionRuntime runtime,
        out WorkflowBoundaryEvidenceWriter evidenceWriter)
    {
        WorkflowController controller = CreateController(runtime);
        evidenceWriter = new WorkflowBoundaryEvidenceWriter();
        return new WorkflowChainRunner(
            new WorkflowResolver(),
            controller,
            new WorkflowEntryGateEvaluator(),
            new WorkflowExitGateEvaluator(),
            new ProductTransferEvaluator(),
            evidenceWriter);
    }

    private static WorkflowController CreateController(FakeTransitionRuntime runtime) =>
        new(new WorkflowResolver(), runtime);

    private static ObservedWorkflowState Completed(WorkflowIdentity workflow) =>
        new(
            workflow,
            WorkflowResolutionState.Completed,
            null,
            [],
            [],
            [$"{workflow}.completed.json"]);

    private static RepositoryObservation Observation(
        IReadOnlyList<ObservedWorkflowState>? workflowStates = null,
        IReadOnlyList<ObservedProduct>? products = null,
        IReadOnlyList<string>? evaluationIntentPaths = null)
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
            new StorageAuthoritySnapshot(StorageAuthorityKind.FilesystemExport, true, "test", [".agents"]),
            workflowStates ?? [],
            products ?? [],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: true, HasWorkingTreeChanges: false, CurrentBranch: "main", Evidence: [".git"]),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: evaluationIntentPaths ?? [],
            StorageVerification: storage);
    }

    private static ObservedProduct Product(
        ProductIdentity identity,
        WorkflowIdentity producer,
        ProductValidationState validation = ProductValidationState.Valid)
    {
        var record = new ProductRecord(
            identity,
            producer,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            "repository-owned test evidence",
            "test",
            [$"{identity}.semantic"],
            $"hash-{identity}",
            ProductFreshness.Fresh,
            validation,
            ProductLifecycle.Active,
            [$"{identity}.semantic"]);
        return new ObservedProduct(record, validation == ProductValidationState.Valid, record.EvidenceLocations);
    }

    private static TransitionRuntimeResult RuntimeResult(RuntimeOutcomeKind outcome) =>
        new(
            outcome,
            outcome == RuntimeOutcomeKind.Completed ? TransitionDurableState.Completed : TransitionDurableState.Failed,
            new WorkflowTransitionIdentity("CreateExecutablePlan"),
            null,
            null,
            null,
            null,
            [],
            outcome.ToString(),
            []);

    private sealed class FakeTransitionRuntime : ITransitionRuntime
    {
        public List<TransitionRuntimeRequest> Requests { get; } = [];

        public TransitionRuntimeResult Result { get; set; } =
            RuntimeResult(RuntimeOutcomeKind.Completed);

        public Task<TransitionRuntimeResult> RunAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }
}

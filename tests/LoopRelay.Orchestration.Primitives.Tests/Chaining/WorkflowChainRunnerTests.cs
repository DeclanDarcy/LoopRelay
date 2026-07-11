using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class WorkflowChainRunnerTests
{
    private static WorkflowChainDefinition TraditionalRoadmapChain =>
        CanonicalWorkflowDefinitionSketches.CreateChains()
            .Single(chain => chain.InitialWorkflow == WorkflowIdentity.TraditionalRoadmap);

    private static WorkflowChainDefinition EvalRoadmapChain =>
        CanonicalWorkflowDefinitionSketches.CreateChains()
            .Single(chain => chain.InitialWorkflow == WorkflowIdentity.EvalRoadmap);

    [Fact]
    public void Canonical_chain_definitions_cover_traditional_and_eval_routes()
    {
        Assert.Equal(
            [WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            TraditionalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
        Assert.Equal(
            [WorkflowIdentity.EvalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            EvalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
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
            TraditionalRoadmapChain,
            Definitions));

        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.True(boundary.CanAdvance);
        Assert.Equal(WorkflowIdentity.Plan, boundary.TargetWorkflow);
        Assert.Equal(WorkflowStopReason.TransitionCompleted, result.StopReason);
        Assert.Single(runtime.Requests);
        Assert.Single(evidence.Records);
    }

    [Fact]
    public async Task Boundary_stops_with_missing_required_input_when_required_semantic_products_are_not_validated()
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
            TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.MissingRequiredInput, result.StopReason);
        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.False(boundary.CanAdvance);
        Assert.False(boundary.ProductTransfer!.Gate.IsSatisfied);
    }

    [Fact]
    public async Task EvalRoadmap_boundary_stops_with_missing_required_input_when_plan_entry_products_are_invalid()
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
            EvalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.MissingRequiredInput, result.StopReason);
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
            TraditionalRoadmapChain,
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
            TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.ChainCompleted, result.StopReason);
        Assert.Equal(WorkflowIdentity.Execute, result.LastWorkflow);
        Assert.Equal(2, result.Boundaries.Count);
    }

    [Fact]
    public async Task Chain_runner_records_workflow_instances_per_driven_workflow()
    {
        FakeTransitionRuntime runtime = new();
        FakeInstanceRecorder recorder = new();
        WorkflowChainRunner runner = CreateRunner(runtime, out _, recorder);
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
            TraditionalRoadmapChain,
            Definitions,
            new RunIdentity("run_chain")));

        Assert.Equal(WorkflowStopReason.TransitionCompleted, result.StopReason);
        Assert.Equal(2, recorder.Begun.Count);
        Assert.Equal(WorkflowIdentity.TraditionalRoadmap, recorder.Begun[0].Workflow);
        Assert.Equal(WorkflowIdentity.Plan, recorder.Begun[1].Workflow);
        Assert.All(recorder.Begun, begun => Assert.Equal("run_chain", begun.RunId));
        Assert.Equal(2, recorder.Completed.Count);
        Assert.Equal((recorder.Begun[0].InstanceId, "Completed", (string?)null), recorder.Completed[0]);
        Assert.Equal((recorder.Begun[1].InstanceId, "Stopped", (string?)"TransitionCompleted"), recorder.Completed[1]);
        TransitionRuntimeRequest runtimeRequest = Assert.Single(runtime.Requests);
        Assert.Equal(new RunIdentity("run_chain"), runtimeRequest.Run);
        Assert.Equal(new WorkflowInstanceIdentity(recorder.Begun[1].InstanceId), runtimeRequest.WorkflowInstance);
    }

    [Fact]
    public async Task Chain_runner_skips_instance_recording_without_a_run_identity()
    {
        FakeTransitionRuntime runtime = new();
        FakeInstanceRecorder recorder = new();
        WorkflowChainRunner runner = CreateRunner(runtime, out _, recorder);
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
            TraditionalRoadmapChain,
            Definitions));

        Assert.Equal(WorkflowStopReason.TransitionCompleted, result.StopReason);
        Assert.Empty(recorder.Begun);
        Assert.Empty(recorder.Completed);
        TransitionRuntimeRequest runtimeRequest = Assert.Single(runtime.Requests);
        Assert.Null(runtimeRequest.Run);
        Assert.Null(runtimeRequest.WorkflowInstance);
    }

    [Fact]
    public async Task Cancelled_controller_stop_still_completes_the_workflow_instance_despite_fired_token()
    {
        FakeTransitionRuntime runtime = new()
        {
            Result = RuntimeResult(RuntimeOutcomeKind.Cancelled),
        };
        FakeInstanceRecorder recorder = new();
        WorkflowChainRunner runner = CreateRunner(runtime, out _, recorder);
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
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        WorkflowChainRunResult result = await runner.RunAsync(
            new WorkflowChainRunRequest(
                new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
                observation,
                TraditionalRoadmapChain,
                Definitions,
                new RunIdentity("run_cancel")),
            cancellation.Token);

        Assert.Equal(WorkflowStopReason.Cancelled, result.StopReason);
        Assert.Equal(2, recorder.Completed.Count);
        Assert.Equal((recorder.Begun[0].InstanceId, "Completed", (string?)null), recorder.Completed[0]);
        Assert.Equal((recorder.Begun[1].InstanceId, "Stopped", (string?)"Cancelled"), recorder.Completed[1]);
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
                    [],
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
    public async Task Controller_stops_with_storage_unusable_when_storage_verification_is_unusable()
    {
        WorkflowController controller = CreateController(new FakeTransitionRuntime());
        RepositoryObservation observation = Observation(
            products:
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]) with
        {
            StorageVerification = new StorageVerificationResult(
                StorageAuthorityKind.FilesystemExport,
                UsableAuthority: false,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions:
                [
                    new ResolutionWarning(
                        WarningCategory.Storage,
                        "storage unusable",
                        "test",
                        "repair storage",
                        []),
                ],
                Evidence: [".agents"]),
        };

        WorkflowControllerResult result = await controller.RunAsync(new WorkflowControllerRequest(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation,
            Definitions));

        Assert.Equal(WorkflowStopReason.StorageUnusable, result.StopReason);
        Assert.Equal(RepositoryClassification.StorageUnusable, result.Resolution.Classification);
        Assert.Null(result.Transition);
    }

    [Fact]
    public async Task Controller_stops_with_missing_required_input_when_stage_transitions_lack_input_products()
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

        Assert.Equal(WorkflowStopReason.MissingRequiredInput, result.StopReason);
        Assert.Null(result.Transition);
    }

    [Theory]
    [InlineData(RuntimeOutcomeKind.MissingRequiredInput, WorkflowStopReason.MissingRequiredInput)]
    [InlineData(RuntimeOutcomeKind.DirtyInputSurface, WorkflowStopReason.DirtyInputSurface)]
    [InlineData(RuntimeOutcomeKind.UnversionedInputSurface, WorkflowStopReason.UnversionedInputSurface)]
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
        out WorkflowBoundaryEvidenceWriter evidenceWriter,
        IWorkflowInstanceRecorder? instanceRecorder = null)
    {
        WorkflowController controller = CreateController(runtime);
        evidenceWriter = new WorkflowBoundaryEvidenceWriter();
        return new WorkflowChainRunner(
            new WorkflowResolver(),
            controller,
            new WorkflowEntryGateEvaluator(),
            new WorkflowExitGateEvaluator(),
            new ProductTransferEvaluator(),
            evidenceWriter,
            instanceRecorder);
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

    private sealed class FakeInstanceRecorder : IWorkflowInstanceRecorder
    {
        private int sequence;

        public List<(string RunId, WorkflowIdentity Workflow, string InstanceId)> Begun { get; } = [];

        public List<(string InstanceId, string Status, string? Outcome)> Completed { get; } = [];

        public Task<string> BeginInstanceAsync(
            string runId,
            WorkflowIdentity workflow,
            CancellationToken cancellationToken)
        {
            sequence++;
            string instanceId = $"wfi_{sequence:D3}";
            Begun.Add((runId, workflow, instanceId));
            return Task.FromResult(instanceId);
        }

        public Task CompleteInstanceAsync(
            string workflowInstanceId,
            string status,
            string? outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Completed.Add((workflowInstanceId, status, outcome));
            return Task.CompletedTask;
        }
    }
}

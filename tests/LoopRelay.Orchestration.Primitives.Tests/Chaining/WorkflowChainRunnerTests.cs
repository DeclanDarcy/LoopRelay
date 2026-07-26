using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class WorkflowChainRunnerTests
{
    private static readonly IReadOnlyList<WorkflowDefinition> Definitions =
        CanonicalWorkflowCatalog.CreateAll();

    private static WorkflowChainDefinition TraditionalRoadmapChain =>
        CanonicalWorkflowCatalog.CreateChains()
            .Single(chain => chain.InitialWorkflow == WorkflowIdentity.TraditionalRoadmap);

    [Fact]
    public void Canonical_chain_definitions_cover_the_product_driven_route()
    {
        Assert.Equal(
            [WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            TraditionalRoadmapChain.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public async Task Progression_transfers_promoted_product_identities_and_preserves_root_run()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);
        Harness harness = new(observation);
        WorkflowRunContext context = NewContext();

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            context,
            FreshAttemptAuthorization.Instance));

        WorkflowBoundaryEvaluation boundary = Assert.Single(result.Boundaries);
        Assert.True(boundary.CanAdvance);
        Assert.Equal(WorkflowIdentity.Plan, boundary.TargetWorkflow);
        Assert.Contains(ProductIdentity.PreparedEpic, result.Decision.ProductTransferManifest);
        Assert.Equal(context.Run, result.Decision.Run);
        Assert.Equal(context.Run, Assert.Single(harness.Instances.Begun).Run);
        Assert.Equal(context.Run, Assert.Single(harness.Boundaries.Captures).Run);
        Assert.Equal(context.Run, Assert.IsType<CanonicalTransitionExecutionContext>(
            Assert.Single(harness.Runtime.Requests).ExecutionContext).Run);
    }

    [Fact]
    public async Task Unsatisfied_successor_gate_stops_with_specific_reason_and_no_runtime_attempt()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            []);
        Harness harness = new(observation);

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            NewContext(),
            FreshAttemptAuthorization.Instance));

        Assert.Equal(WorkflowStopReason.MissingRequiredInput, result.StopReason);
        Assert.False(Assert.Single(result.Boundaries).CanAdvance);
        Assert.Empty(harness.Runtime.Requests);
        Assert.Empty(harness.Instances.Begun);
    }

    [Fact]
    public async Task Required_effects_pending_prevents_chain_progression_without_redundant_observation()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        harness.Runtime.Result = RuntimeResult(
            RuntimeOutcomeKind.EffectsPending,
            TransitionDurableState.EffectsPending,
            effectsPending: true);
        harness.Effects.Result = new TransitionEffectCoordinationResult(
            RequiredEffectsPending: true,
            Failed: false,
            "push pending",
            ["effect:push"]);

        WorkflowChainRunResult result = await harness.Runner.RunAsync(new WorkflowChainRunRequest(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            observation,
            TraditionalRoadmapChain,
            Definitions,
            NewContext(),
            FreshAttemptAuthorization.Instance));

        Assert.Equal(WorkflowStopReason.RequiredEffectsPending, result.StopReason);
        Assert.True(result.Decision.RequiredEffectsPending);
        Assert.Equal(1, harness.Effects.CallCount);
        // The controller no longer performs a post-attempt observation solely to populate a
        // consumerless field, and this path never reaches the chain runner's own boundary-crossing
        // re-observation (that only fires when advancing past a *completed* workflow). Zero
        // observation calls is the correct, exact count here.
        Assert.Equal(0, harness.Observations.CallCount);
    }

    [Fact]
    public async Task Boundary_evidence_failure_fails_closed()
    {
        RepositoryObservation observation = Observation(
            [Completed(WorkflowIdentity.TraditionalRoadmap)],
            [
                Product(ProductIdentity.PreparedEpic, WorkflowIdentity.TraditionalRoadmap),
                Product(ProductIdentity.MilestoneSpecificationSet, WorkflowIdentity.TraditionalRoadmap),
            ]);
        Harness harness = new(observation);
        harness.Boundaries.FailAppend = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Runner.RunAsync(
            new WorkflowChainRunRequest(
                new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
                observation,
                TraditionalRoadmapChain,
                Definitions,
                NewContext(),
                FreshAttemptAuthorization.Instance)));
    }

    [Fact]
    public async Task Kernel_owns_authorization_reobservation_decision_facts_and_passive_budget_exhaustion()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        var decisions = new RecordingKernelDecisionStore();
        var kernel = new OrchestrationKernel(harness.Runner, harness.Observations,
            new DurableKernelAttemptAuthorizationSelector(), decisions);
        WorkflowRunContext context = NewContext();

        KernelResult result = await kernel.RunAsync(new KernelCommand(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain), observation,
            TraditionalRoadmapChain, CanonicalWorkflowCatalog.Current, context, 1));

        Assert.Equal(WorkflowStopReason.Waiting, result.StopReason);
        Assert.Equal(RuntimeOutcomeKind.Waiting, result.Outcome);
        Assert.Equal(context.Run, result.RootRun);
        Assert.Equal(64, result.SnapshotIdentity.Length);
        Assert.IsType<FreshAttemptAuthorization>(Assert.Single(harness.Runtime.Requests).Authorization);
        KernelDecisionFact decision = Assert.Single(decisions.Decisions);
        Assert.Equal(context.Run, decision.RootRun);
        Assert.Equal(CanonicalWorkflowCatalog.Current.Identity, decision.CatalogIdentity);
        // Exactly one observation for this single completed-attempt cycle: the kernel's own
        // re-observation at the cycle boundary (OrchestrationKernel.RunAsync). The controller's
        // former post-attempt observation, which only fed the consumerless ObservationAfter
        // field, is gone.
        Assert.Equal(1, harness.Observations.CallCount);
    }

    [Fact]
    public async Task Kernel_multi_cycle_run_sequences_stop_reasons_and_drops_redundant_post_attempt_observation()
    {
        RepositoryObservation observation = Observation();
        Harness harness = new(observation);
        // Cycle 0: attempt completes outright -> kernel continues to the next cycle.
        harness.Runtime.Sequence.Enqueue(RuntimeResult());
        // Cycle 1: attempt requires effect coordination that remains pending -> kernel stops.
        harness.Runtime.Sequence.Enqueue(RuntimeResult(
            RuntimeOutcomeKind.EffectsPending,
            TransitionDurableState.EffectsPending,
            effectsPending: true));
        harness.Effects.Result = new TransitionEffectCoordinationResult(
            RequiredEffectsPending: true,
            Failed: false,
            "push pending",
            ["effect:push"]);
        var decisions = new RecordingKernelDecisionStore();
        var kernel = new OrchestrationKernel(harness.Runner, harness.Observations,
            new DurableKernelAttemptAuthorizationSelector(), decisions);
        WorkflowRunContext context = NewContext();

        KernelResult result = await kernel.RunAsync(new KernelCommand(
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain), observation,
            TraditionalRoadmapChain, CanonicalWorkflowCatalog.Current, context, ObservationBudget: 5));

        // Scripted sequence of stop reasons across the two real cycles, recorded in kernel
        // decision facts (one per cycle actually executed, before the loop decides to stop).
        Assert.Equal(
            [WorkflowStopReason.TransitionCompleted, WorkflowStopReason.RequiredEffectsPending],
            decisions.Decisions.Select(decision => decision.Outcome));
        Assert.Equal(WorkflowStopReason.RequiredEffectsPending, result.StopReason);
        Assert.Equal(RuntimeOutcomeKind.EffectsPending, result.Outcome);
        // Both scripted attempts actually ran (the generous budget of 5 proves the loop stopped
        // because of the second attempt's outcome, not because the budget was exhausted).
        Assert.Equal(2, harness.Runtime.Requests.Count);
        // Exactly one observation total: the kernel's cycle-boundary re-observation after cycle 0
        // (StopReason == TransitionCompleted). The per-attempt controller observation that used to
        // fire on *both* cycles is gone, so the count drops by exactly one per attempt (2 -> 0)
        // while the load-bearing kernel-boundary observation (0 -> 1 in this scenario) is untouched.
        Assert.Equal(1, harness.Observations.CallCount);
    }

    private sealed class Harness
    {
        public Harness(RepositoryObservation observation)
        {
            Runtime = new FakeTransitionRuntime();
            Effects = new FakeEffectCoordinator();
            Observations = new StaticObservationSource(observation);
            Instances = new FakeInstanceRecorder();
            Boundaries = new RecordingBoundaryStore();
            var controller = new WorkflowController(
                new WorkflowResolver(), Runtime, Effects);
            Runner = new WorkflowChainRunner(
                new WorkflowResolver(),
                controller,
                new WorkflowEntryGateEvaluator(),
                new WorkflowExitGateEvaluator(),
                new ProductTransferEvaluator(),
                new WorkflowBoundaryEvidenceWriter(Boundaries),
                Instances,
                Observations);
        }

        public FakeTransitionRuntime Runtime { get; }
        public FakeEffectCoordinator Effects { get; }
        public StaticObservationSource Observations { get; }
        public FakeInstanceRecorder Instances { get; }
        public RecordingBoundaryStore Boundaries { get; }
        public WorkflowChainRunner Runner { get; }
    }

    private static WorkflowRunContext NewContext() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        new PolicyIdentity("policy_test"),
        new RuntimeProfileIdentity("runtime_test"),
        new PromptPolicyProfileIdentity("prompt_policy_test"));

    private static TransitionRuntimeResult RuntimeResult(
        RuntimeOutcomeKind outcome = RuntimeOutcomeKind.Completed,
        TransitionDurableState state = TransitionDurableState.Completed,
        bool effectsPending = false) => new(
        outcome,
        state,
        new WorkflowTransitionIdentity("CreateExecutablePlan"),
        null, null, null, null, [], outcome.ToString(), [],
        TransitionRunIdentity.New(),
        AttemptIdentity.New(),
        AttemptCompleted: true,
        RequiredEffectsPending: effectsPending);

    private sealed class FakeTransitionRuntime : ITransitionRuntime
    {
        public List<TransitionRuntimeRequest> Requests { get; } = [];
        public TransitionRuntimeResult Result { get; set; } = RuntimeResult();

        /// <summary>
        /// Optional scripted per-call outcomes for multi-cycle scenarios. Dequeued in order;
        /// falls back to <see cref="Result"/> once exhausted (or if never populated).
        /// </summary>
        public Queue<TransitionRuntimeResult> Sequence { get; } = new();

        public Task<TransitionRuntimeResult> RunAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            TransitionRuntimeResult result = Sequence.Count > 0 ? Sequence.Dequeue() : Result;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeEffectCoordinator : ITransitionEffectCoordinator
    {
        public int CallCount { get; private set; }
        public TransitionEffectCoordinationResult Result { get; set; } =
            new(false, false, "effects complete", []);

        public Task<TransitionEffectCoordinationResult> CoordinateAsync(
            TransitionRunIdentity transitionRun,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class StaticObservationSource(RepositoryObservation observation)
        : ICanonicalRepositoryObservationSource
    {
        public int CallCount { get; private set; }

        public Task<RepositoryObservation> ObserveAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(observation);
        }
    }

    private sealed class FakeInstanceRecorder : IWorkflowInstanceRecorder
    {
        public List<(RunIdentity Run, WorkflowIdentity Workflow, WorkflowInstanceIdentity Instance)> Begun { get; } = [];

        public Task<WorkflowInstanceIdentity> BeginInstanceAsync(
            RunIdentity run,
            WorkflowIdentity workflow,
            CancellationToken cancellationToken)
        {
            WorkflowInstanceIdentity instance = WorkflowInstanceIdentity.New();
            Begun.Add((run, workflow, instance));
            return Task.FromResult(instance);
        }

        public Task CompleteInstanceAsync(
            WorkflowInstanceIdentity workflowInstance,
            string status,
            string? outcome,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingBoundaryStore : IChainBoundaryEvidenceStore
    {
        public bool FailAppend { get; set; }
        public List<ChainBoundaryEvidenceCapture> Captures { get; } = [];

        public Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken)
        {
            if (FailAppend)
            {
                throw new InvalidOperationException("boundary persistence failed");
            }

            Captures.Add(capture);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingKernelDecisionStore : IKernelDecisionStore
    {
        public List<KernelDecisionFact> Decisions { get; } = [];
        public Task AppendAsync(KernelDecisionFact decision, CancellationToken cancellationToken)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }
    }

    private static ObservedWorkflowState Completed(WorkflowIdentity workflow) => new(
        workflow,
        WorkflowResolutionState.Completed,
        null,
        [], [],
        [$"{workflow}.completed.json"]);

    private static RepositoryObservation Observation(
        IReadOnlyList<ObservedWorkflowState>? workflowStates = null,
        IReadOnlyList<ObservedProduct>? products = null)
    {
        var verification = new StorageVerificationResult(
            StorageAuthorityKind.CanonicalSqlite,
            true,
            [], [], [], [], [], [], [],
            ["canonical"]);
        return new RepositoryObservation(
            "repo",
            new StorageAuthoritySnapshot(StorageAuthorityKind.CanonicalSqlite, true, "test", ["canonical"]),
            workflowStates ?? [],
            products ?? [],
            [], [], [],
            new ObservedGitFacts(true, false, "main", [".git"]),
            [], [],
            verification);
    }

    private static ObservedProduct Product(ProductIdentity identity, WorkflowIdentity producer)
    {
        var product = new ProductRecord(
            identity,
            producer,
            new WorkflowTransitionIdentity($"Produce{identity.Value}"),
            [WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            "repository",
            "test",
            [$"{identity.Value}.md"],
            $"causal-{identity.Value}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity.Value}.md"]);
        return new ObservedProduct(product, true, product.EvidenceLocations);
    }
}
